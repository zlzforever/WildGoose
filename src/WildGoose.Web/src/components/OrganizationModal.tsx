import { Col, Form, Input, Modal, Row, Select, TreeSelect, TreeSelectProps, message } from 'antd'

import TextArea from 'antd/es/input/TextArea'
import { addOrganization, updateOrganization, getOrganization, getSubOrganizationList } from '../services/wildgoods/api'
import { useEffect, useState } from 'react'
import type { DefaultOptionType } from 'antd/es/select'

export interface OrganizationModalProps {
  id?: string
  parent?: {
    id: string
    name: string
  }
  open?: boolean
  onClose?: () => void
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  onOk?: (values: any, originParentId?: string) => void
}

const OrganizationModal: React.FC<OrganizationModalProps> = (props) => {
  const [form] = Form.useForm<{
    id: string
    name: string
    code: string
    address: string
    description: string
    parentId: string
    scope: string[]
    administrator: {
      id: string
      name: string
    }[]
  }>()
  const [parentTreeData, setParentTreeData] = useState<Omit<DefaultOptionType, 'label'>[]>([])
  const [originParentId, setOriginParentId] = useState<string>('')
  const [parentId, setParentId] = useState('')
  const title = props.id ? '编辑机构' : '添加机构'

  // 初始化机构选择器
  useEffect(() => {
    const init = async () => {
      const res = await getSubOrganizationList('')
      const parentOrganizations = res.data as { id: string; parentId: string; name: string; hasChild: boolean }[]
      if (parentOrganizations) {
        const data = [] as never[]
        parentOrganizations.map((x) => {
          data.push({
            id: x.id,
            pId: x.parentId,
            value: x.id,
            title: x.name,
            isLeaf: !x.hasChild,
            childrean: [],
          } as never)
        })
        setParentTreeData(data)
      } else {
        setParentTreeData([])
      }
    }
    init()
  }, [])

  useEffect(() => {
    const init = async () => {
      form.resetFields()

      let data
      // 创建
      if (!props.id) {
        // 上级机构初始化
        if (props.parent && props.parent.id) {
          data = {
            parentId: props.parent.id,
            parentName: props.parent.name,
          }
        } else {
          data = {
            parentId: '',
            parentName: '',
          }
        }
      }
      // 编辑
      else {
        const res = await getOrganization(props.id)
        data = res.data
      }

      // 保留原始的级机构信息
      setOriginParentId(data.parentId)
      // 使用名称， 防止因为机构树未加载而显示 ID
      data.parentId = data.parentName
      form.setFieldsValue(data)
    }
    init()
  }, [props.id, props.parent, form])

  const onOk = () => {
    form.validateFields().then(async () => {
      const values = form.getFieldsValue()
      let res
      if (props.id) {
        if (values.parentId === props.id) {
          message.error('上级机构不能为自身')
          return
        }
        if (parentId) {
          values.parentId = parentId
        }
        res = await updateOrganization(props.id, values)
      } else {
        if (parentId) {
          values.parentId = parentId
        }
        res = await addOrganization(values)
      }
      if (props.onClose) {
        props.onClose()
      }
      if (props.onOk) {
        props.onOk(res.data, originParentId)
      }
    })
  }

  const onParentTreeLoadData: TreeSelectProps['loadData'] = async ({ id }) => {
    const res = await getSubOrganizationList(id)
    const subOrganizations = res.data as []
    if (subOrganizations) {
      const data = subOrganizations.map((x: { id: string; parentId: string; name: string; hasChild: boolean }) => {
        const index = parentTreeData.findIndex((y) => y.id === x.id)
        if (index === -1) {
          return {
            id: x.id,
            pId: x.parentId,
            value: x.id,
            title: x.name,
            isLeaf: !x.hasChild,
          }
        }
      })
      const value = parentTreeData.concat(data)
      setParentTreeData(value)
    }
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
              <Form.Item name="name" label="名称" rules={[{ required: true, message: '请输入名称' }]}>
                <Input placeholder="请输入名称" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="code" label="编号">
                <Input placeholder="请输入编号" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="parentId" label="上级部门">
                <TreeSelect
                  allowClear={true}
                  treeLine
                  treeData={parentTreeData}
                  treeDataSimpleMode
                  dropdownStyle={{ maxHeight: 400, overflow: 'auto' }}
                  loadData={onParentTreeLoadData}
                  onChange={(v) => {
                    setParentId(v)
                  }}
                  placeholder="请选择上级部门"
                />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="scope" label="数据范围">
                <Select placeholder="请输入数据范围" mode="tags" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="address" label="地址">
                <Input placeholder="请输入地址" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="description" label="描述">
                <TextArea style={{ width: '100%' }} placeholder="请输入描述" />
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

export default OrganizationModal
