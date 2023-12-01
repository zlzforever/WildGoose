import { Form, Row, Col, Input, Modal, TreeSelect, TreeSelectProps, Checkbox, DatePicker, Select, SelectProps } from 'antd'
import { useEffect, useState } from 'react'
import { getSubOrganizationList, getUser, updateUser, addUser, getAssignableRoles } from '../services/wildgoods/api'
import * as dayjs from 'dayjs'

export interface UserModalProps {
  user: {
    id: string
    organization?: {
      id: string
      pId: string
      value: string
      title: string
      isLeaf: boolean
    }
  }
  open?: boolean
  onClose?: () => void
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  onOk?: () => void
}

const UserModal: React.FC<UserModalProps> = (props) => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const [form] = Form.useForm<any>()
  //   const [form] = Form.useForm<AddUserType>()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const [organizationTreeData, setOrganizationTreeData] = useState<any[]>([])
  const [organizationTreeSelectedKeys, setOrganizationTreeSelectedKeys] = useState<string[]>()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const cache = {} as any
  if (props.user.organization) {
    cache[props.user.organization.id] = props.user.organization
  }
  const [organizationTreeDict, setOrganizationTreeDict] = useState(cache)
  const [roleOptions, setRoleOptions] = useState<SelectProps['options']>()

  const title = props.user && props.user.id ? '编辑' : '添加'

  // 初始化机构选择器
  useEffect(() => {
    const init = async () => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      let data: any[]
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      let cache = {} as any
      const res = await getSubOrganizationList('')
      const organizations = res.data as { id: string; parentId: string; name: string; hasChild: boolean }[]
      if (organizations) {
        data = organizations.map((x) => {
          const node = {
            id: x.id,
            pId: x.parentId,
            value: x.id,
            title: x.name,
            isLeaf: !x.hasChild,
          }
          cache[node.id] = node
          return node
        })
      } else {
        data = []
      }

      const res1 = await getAssignableRoles()
      const roles = (res1.data as { id: string; name: string }[]).map((x) => {
        return {
          value: x.id,
          label: x.name,
        }
      })

      // 若初始查询出的机构不含有传入的机构， 则把传入的机构并入数组
      if (props.user.organization?.id && data.findIndex((item) => item.id === props.user.organization?.id) === -1) {
        data.push(props.user.organization)
        cache[props.user.organization.id] = props.user.organization
      }

      form.resetFields()

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      let values: any
      // 创建
      if (!props.user.id) {
        // 上级机构初始化
        if (props.user.organization?.id) {
          values = {
            organizations: [props.user.organization.id],
          }
        } else {
          values = {
            organizations: [],
          }
        }
      }
      // 编辑
      else {
        const res = await getUser(props.user.id)
        values = res.data
        values.departureTime = values.departureTime ? dayjs.unix(values.departureTime) : null

        const result = contactOrganizations(data, values.organizations, cache)
        if (result) {
          data = result.organizations
          cache = result.cache
        }
        values.organizations = values.organizations.map((x: { id: string }) => x.id)

        // 若有角色不是当前用户可授于角色（是别人授于的）也要能显示
        values.roles.map((x: { id: string; name: string }) => {
          if (roles.findIndex((y) => y.value === x.id) === -1) {
            roles.push({
              value: x.id,
              label: x.name,
            })
          }
        })
        values.roles = values.roles.map((x: { id: string }) => x.id)
      }

      setRoleOptions(roles)
      setOrganizationTreeData(data)
      setOrganizationTreeDict(cache)

      form.setFieldsValue(values)
    }
    init()
  }, [props.user.organization, form, props.user])

  //   useEffect(() => {
  //     const init = async () => {
  //       form.resetFields()

  //       // eslint-disable-next-line @typescript-eslint/no-explicit-any
  //       let data: any
  //       // 创建
  //       if (!props.user.id) {
  //         // 上级机构初始化
  //         if (props.user.organization?.id) {
  //           data = {
  //             organizations: [props.user.organization.id],
  //           }
  //         }
  //       }
  //       // 编辑
  //       else {
  //         const res = await getUser(props.user.id)
  //         data = res.data
  //         const result = contactOrganizations(data.organizations, organizationTreeDict)
  //         if (result) {
  //           setOrganizationTreeData(result.value)
  //           setOrganizationTreeDict(result.organizationTreeDict)
  //         }
  //       }

  //       form.setFieldsValue(data)
  //     }
  //     init()
  //   }, [props.user, form])

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const contactOrganizations = (organizations: any[], subOrganizations: any[], cache: any) => {
    let data = subOrganizations.map((x: { id: string; parentId: string; name: string; hasChild: boolean }) => {
      const node = {
        id: x.id,
        pId: x.parentId,
        value: x.id,
        title: x.name,
        isLeaf: !x.hasChild,
      }

      const origin = cache[x.id]
      if (!origin) {
        cache[x.id] = node
        return node
      } else {
        origin.pId = node.pId
        origin.value = node.value
        origin.title = node.title
        origin.isLeaf = node.isLeaf
        return null
      }
    })

    data = data.filter((x) => x)
    if (data.length) {
      const value = organizations.concat(data)
      return {
        organizations: value,
        cache,
      }
    }
    return null
  }

  const onOrganizationLoadData: TreeSelectProps['loadData'] = async ({ id }) => {
    const res = await getSubOrganizationList(id)
    const subOrganizations = res.data as []
    if (subOrganizations) {
      const result = contactOrganizations(organizationTreeData, subOrganizations, organizationTreeDict)
      if (result) {
        setOrganizationTreeData(result.organizations)
        setOrganizationTreeDict(result.cache)
      }
    }
  }

  const onOk = () => {
    form.validateFields().then(async () => {
      const values = form.getFieldsValue()

      // 编辑
      if (props.user.id) {
        await updateUser(props.user.id, values)
      }
      // 新增
      else {
        if (organizationTreeSelectedKeys) {
          values.organizations = organizationTreeSelectedKeys
        } else {
          if (props.user.organization?.id) {
            values.organizations = [props.user.organization?.id]
          } else {
            values.organizations = []
          }
        }
        await addUser(values)
      }
      if (props.onClose) {
        props.onClose()
      }
      if (props.onOk) {
        props.onOk()
      }
    })
  }
  return (
    <>
      <Modal
        title={title}
        styles={{
          body: {
            paddingBottom: 80,
          },
        }}
        width={720}
        open={props.open}
        onOk={onOk}
        onCancel={() => {
          if (props.onClose) {
            props.onClose()
          }
        }}>
        <Form layout="vertical" form={form}>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="userName" label="帐号" rules={[{ required: true, message: '请输入帐号' }]}>
                <Input placeholder="请输入帐号" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="name" label="姓名" rules={[{ required: true, message: '请输入姓名' }]}>
                <Input placeholder="请输入姓名" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="phoneNumber" label="电话">
                <Input placeholder="请输入电话" />
              </Form.Item>
            </Col>
          </Row>
          {props.user.id ? (
            <>
              <Row gutter={16}>
                <Col span={24}>
                  <Form.Item name="code" label="编号">
                    <Input placeholder="请输入编号" />
                  </Form.Item>
                </Col>
              </Row>
              <Row gutter={16}>
                <Col span={24}>
                  <Form.Item name="email" label="邮箱">
                    <Input placeholder="请输入邮箱" />
                  </Form.Item>
                </Col>
              </Row>
              <Row gutter={16}>
                <Col span={24}>
                  <Form.Item name="title" label="职位">
                    <Input placeholder="请输入职位" />
                  </Form.Item>
                </Col>
              </Row>
            </>
          ) : (
            <Row gutter={16}>
              <Col span={24}>
                <Form.Item name="password" label="密码" rules={[{ required: true, message: '请输入密码' }]}>
                  <Input placeholder="请输入密码" />
                </Form.Item>
              </Col>
            </Row>
          )}
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="organizations" label="部门">
                <TreeSelect
                  allowClear={true}
                  treeLine
                  multiple={true}
                  treeData={organizationTreeData}
                  onChange={(v) => {
                    setOrganizationTreeSelectedKeys(v)
                  }}
                  treeDataSimpleMode
                  dropdownStyle={{ maxHeight: 400, overflow: 'auto' }}
                  loadData={onOrganizationLoadData}
                  placeholder="部门"
                />
              </Form.Item>
            </Col>
          </Row>
          {props.user.id ? (
            <>
              <Row gutter={16}>
                <Col span={24}>
                  <Form.Item name="roles" label="角色">
                    <Select mode="multiple" options={roleOptions} />
                  </Form.Item>
                </Col>
              </Row>
              <Row gutter={16}>
                <Col span={24}>
                  <Form.Item name="departureTime" label="离职时间">
                    <DatePicker />
                  </Form.Item>
                </Col>
              </Row>
              <Row gutter={16}>
                <Col span={24}>
                  <Form.Item name="hiddenSensitiveData" label="隐藏敏感信息" valuePropName="checked">
                    <Checkbox />
                  </Form.Item>
                </Col>
              </Row>
            </>
          ) : (
            <></>
          )}
        </Form>
      </Modal>
    </>
  )
}

export default UserModal
