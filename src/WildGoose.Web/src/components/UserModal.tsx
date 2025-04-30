import {
  Form,
  Row,
  Col,
  Input,
  Modal,
  TreeSelect,
  TreeSelectProps,
  Checkbox,
  DatePicker,
  Select,
  SelectProps,
  message,
} from "antd"
import { useEffect, useState } from "react"
import {
  getSubOrganizationList,
  getUser,
  updateUser,
  addUser,
  getAssignableRoles,
} from "../services/wildgoose/api"
import * as dayjs from "dayjs"

const phoneValidator = (_: any, value: any, callback: any) => {
  if (value) {
    const reg: any = /^(13[0-9]|14[01456879]|15[0-35-9]|16[2567]|17[0-8]|18[0-9]|19[0-35-9])\d{8}$/
    if (reg.test(value)) {
      return callback()
    }
    return Promise.reject(new Error("手机号无效"))
  }
  return callback()
}

export interface UserProps {
  id?: string
  organization?: OrganizationDto
}

export interface UserModalProps extends UserProps {
  open?: boolean
  onClose?: () => void
  onOk?: (user: UserDto) => void
}

const UserModal: React.FC<UserModalProps> = (props) => {
  const [form] = Form.useForm<UpdateUserDto>()
  const [organizationTreeData, setOrganizationTreeData] = useState<OrganizationTreeNode[]>([])
  // const [organizationTreeSelectedKeys, setOrganizationTreeSelectedKeys] = useState<string[]>()
  const [organizationTreeDict, setOrganizationTreeDict] = useState<
    Dictionary<OrganizationTreeNode>
  >({})
  const [roleOptions, setRoleOptions] = useState<SelectProps["options"]>()

  const title = props.id ? "编辑用户" : "添加用户"

  // 初始化机构选择器
  useEffect(() => {
    const init = async () => {
      if (!props.open) {
        return
      }
      const cache: Dictionary<OrganizationTreeNode> = {}
      const res = await getSubOrganizationList("")
      const subOrganizations = (res.data as OrganizationDto[]) ?? []
      const organizations = subOrganizations.map((x) => {
        const node: OrganizationTreeNode = {
          id: x.id,
          pId: x.parentId,
          value: x.id,
          title: x.name,
          isLeaf: !x.hasChild,
        }
        cache[x.id] = node
        return node
      })
      // 若初始查询出的机构不含有传入的机构， 则把传入的机构并入数组
      if (
        props.organization &&
        organizations.findIndex((item) => item.id === props.organization?.id) === -1
      ) {
        const node = {
          id: props.organization.id,
          pId: props.organization.parentId,
          value: props.organization.id,
          title: props.organization.name,
          isLeaf: !props.organization.hasChild,
        }
        cache[props.organization.id] = node
        organizations.push(node)
      }

      form.resetFields()

      // 创建
      if (!props.id) {
        const user: AddUserDto = {
          organizations: [],
          password: "",
          userName: "",
        }
        // 上级机构初始化
        if (props.organization?.id) {
          user.organizations = [props.organization.id]
        } else {
          //
        }

        form.setFieldsValue(user)
      }
      // 编辑
      else {
        const res1 = await getAssignableRoles()
        const roles = (res1.data as RoleBasicDto[]).map((x) => {
          return {
            value: x.id,
            label: x.name,
          }
        })

        const values: UpdateUserDto = {
          organizations: [],
          roles: [],
          userName: "",
          hiddenSensitiveData: false,
        }
        const res = await getUser(props.id)
        if (!res.data) {
          message.error("用户信息为空")
          return
        }
        const d = res.data as UserDetailDto
        values.code = d.code
        values.email = d.email
        values.hiddenSensitiveData = d.hiddenSensitiveData
        values.name = d.name
        values.phoneNumber = d.phoneNumber
        values.title = d.title
        values.userName = d.userName

        if (d.departureTime) {
          values.departureTime = dayjs.unix(d.departureTime)
        }

        concatOrganizations(organizations, d.organizations, cache)

        values.organizations = d.organizations.map((x) => x.id)

        // 若有角色不是当前用户可授于角色（是别人授于的）也要能显示
        d.roles.map((x) => {
          if (roles.findIndex((y) => y.value === x.id) === -1) {
            roles.push({
              value: x.id,
              label: x.name,
            })
          }
        })
        values.roles = d.roles.map((x) => x.id)

        form.setFieldsValue(values)

        setRoleOptions(roles)
      }

      setOrganizationTreeData(organizations)
      setOrganizationTreeDict(cache)
    }
    init()
  }, [props.organization, form, props.id, props.open])

  const concatOrganizations = (
    treeData: OrganizationTreeNode[],
    subOrganizations: OrganizationDto[],
    cache: Dictionary<OrganizationTreeNode>
  ) => {
    subOrganizations.map((x) => {
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
        treeData.push(node)
      } else {
        origin.pId = node.pId
        origin.value = node.value
        origin.title = node.title
        origin.isLeaf = node.isLeaf
      }
    })
  }

  const onOrganizationLoadData: TreeSelectProps["loadData"] = async ({ id }) => {
    const res = await getSubOrganizationList(id)
    const subOrganizations = res.data as OrganizationDto[]
    if (subOrganizations && subOrganizations.length > 0) {
      concatOrganizations(organizationTreeData, subOrganizations, organizationTreeDict)
      setOrganizationTreeData([...organizationTreeData])
      setOrganizationTreeDict(organizationTreeDict)
    }
  }

  const onOk = async () => {
    const result = await form.validateFields()
    if (result) {
      const values = form.getFieldsValue()
      let user
      // 编辑
      if (props.id) {
        user = (await updateUser(props.id, values)).data
      }
      // 新增
      else {
        user = (await addUser(values)).data
      }
      if (props.onOk) {
        props.onOk(user)
      }
    }
    // form.validateFields().then(async () => {})
  }
  return (
    <>
      <Modal
        title={title}
        width={720}
        maskClosable={false}
        open={props.open}
        onOk={onOk}
        onCancel={() => {
          if (props.onClose) {
            props.onClose()
          }
        }}
      >
        <Form layout="vertical" form={form}>
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="userName"
                label="帐号"
                rules={[{ required: true, message: "请输入帐号" }]}
              >
                <Input placeholder="请输入帐号" maxLength={36} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="name"
                label="姓名"
                rules={[{ required: true, message: "请输入姓名" }]}
              >
                <Input placeholder="请输入姓名" maxLength={256} />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="phoneNumber"
                label="手机号"
                rules={[{ required: true, message: "请输入手机号" }, { validator: phoneValidator }]}
              >
                <Input placeholder="请输入手机号" maxLength={11} />
              </Form.Item>
            </Col>
            {props.id ? (
              <>
                <Col span={12}>
                  <Form.Item name="code" label="编号">
                    <Input placeholder="请输入编号" maxLength={36} />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item
                    name="email"
                    label="邮箱"
                    rules={[{ type: "email", message: "邮箱无效" }]}
                  >
                    <Input placeholder="请输入邮箱" maxLength={256} />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item name="title" label="职位">
                    <Input placeholder="请输入职位" maxLength={256} />
                  </Form.Item>
                </Col>
              </>
            ) : (
              <Col span={12}>
                <Form.Item
                  name="password"
                  label="密码"
                  rules={[{ required: true, message: "请输入密码" }]}
                >
                  <Input placeholder="请输入密码" maxLength={32} />
                </Form.Item>
              </Col>
            )}
            <Col span={12}>
              <Form.Item name="organizations" label="部门">
                <TreeSelect
                  allowClear={true}
                  treeLine
                  multiple={true}
                  treeData={organizationTreeData}
                  // onChange={(v) => {
                  //   setOrganizationTreeSelectedKeys(v)
                  // }}
                  treeDataSimpleMode
                  dropdownStyle={{ maxHeight: 400, overflow: "auto" }}
                  loadData={onOrganizationLoadData}
                  placeholder="部门"
                />
              </Form.Item>
            </Col>
            {props.id ? (
              <>
                <Col span={12}>
                  <Form.Item name="roles" label="角色">
                    <Select mode="multiple" options={roleOptions} />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item name="departureTime" label="离职时间">
                    <DatePicker />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item
                    name="hiddenSensitiveData"
                    label="隐藏敏感信息"
                    valuePropName="checked"
                  >
                    <Checkbox />
                  </Form.Item>
                </Col>
              </>
            ) : (
              <></>
            )}
          </Row>
        </Form>
      </Modal>
    </>
  )
}

export default UserModal
