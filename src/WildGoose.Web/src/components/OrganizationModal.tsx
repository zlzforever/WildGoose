import { Col, Form, Input, Modal, Row, Select, TreeSelect, TreeSelectProps, message } from 'antd'

import TextArea from 'antd/es/input/TextArea'
import { addOrganization, updateOrganization, getOrganization, getSubOrganizationList } from '../services/wildgoods/api'
import { useEffect, useState } from 'react'
import AceEditor from 'react-ace'
import 'ace-builds/src-noconflict/theme-monokai'
import 'ace-builds/src-noconflict/mode-json'

export interface OrganizationModalProps {
  id?: string
  parent?: OrganizationDto
  open?: boolean
  onClose?: () => void
  onOk?: (values: OrganizationDto, originParentId?: string) => void
}

const OrganizationModal: React.FC<OrganizationModalProps> = (props) => {
  const [form] = Form.useForm<Organization>()
  const [parentTreeData, setParentTreeData] = useState<OrganizationTreeNode[]>([])
  const [parentTreeDict, setParentTreeDict] = useState<Dictionary<OrganizationTreeNode>>({})
  const [originParentId, setOriginParentId] = useState<string>('')
  const title = props.id ? '编辑机构' : '添加机构'

  // 初始化机构选择器
  useEffect(() => {
    if (!props.open) {
      return
    }

    const init = async () => {
      form.resetFields()

      const res = await getSubOrganizationList('')
      const subOrganizations = (res.data as OrganizationDto[]) ?? []
      const cache: Dictionary<OrganizationTreeNode> = {}
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

      let data = { parentId: '' }
      // 创建
      if (!props.id) {
        // 上级机构初始化
        if (props.parent) {
          data.parentId = props.parent.id
          let parent = cache[data.parentId]
          if (!parent) {
            parent = {
              id: props.parent.id,
              pId: props.parent.parentId,
              title: props.parent.name,
              value: props.parent.id,
              isLeaf: !props.parent.hasChild,
            }
            cache[parent.id] = parent
            organizations.push(parent)
          }
        }
      }
      // 编辑
      else {
        const res = await getOrganization(props.id)
        data = res.data
        const serverParent = res.data.parent
        if (serverParent) {
          let parent = cache[serverParent.id]
          if (!parent) {
            parent = {
              id: serverParent.id,
              pId: serverParent.parentId,
              title: serverParent.name,
              value: serverParent.id,
              isLeaf: !serverParent.hasChild,
            }
            cache[serverParent.id] = parent
            organizations.push(parent)
          } else {
            parent.pId = serverParent.parentId
            parent.title = serverParent.name
            parent.isLeaf = !serverParent.hasChild
          }
          data.parentId = serverParent.id
        } else {
          data.parentId = ''
        }
        setOriginParentId(data.parentId)
      }

      form.setFieldsValue(data)

      setParentTreeDict(cache)
      setParentTreeData(organizations)
    }
    init()
  }, [form, props.id, props.open, props.parent])

  const onOk = () => {
    form.validateFields().then(async () => {
      const values = form.getFieldsValue()
      let res
      if (props.id) {
        if (values.parentId === props.id) {
          message.error('上级机构不能为自身')
          return
        }
        res = await updateOrganization(props.id, values)
      } else {
        res = await addOrganization(values)
      }

      if (props.onOk) {
        props.onOk(res.data, originParentId)
      }
    })
  }

  const onParentTreeLoadData: TreeSelectProps['loadData'] = async ({ id }) => {
    const res = await getSubOrganizationList(id)
    const subOrganizations = (res.data as OrganizationDto[]) ?? []
    const data: OrganizationTreeNode[] = []
    subOrganizations.map((x) => {
      const orgin = parentTreeDict[x.id]
      if (!orgin) {
        const node: OrganizationTreeNode = {
          id: x.id,
          pId: x.parentId,
          value: x.id,
          title: x.name,
          isLeaf: !x.hasChild,
        }
        parentTreeDict[x.id] = node
        data.push(node)
      } else {
        orgin.pId = x.parentId
        orgin.title = x.name
        orgin.isLeaf = !x.hasChild
      }
    })

    if (data.length) {
      setParentTreeData(parentTreeData.concat(data))
      setParentTreeDict(parentTreeDict)
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
                  // onChange={(v) => {
                  //   setParentId(v)
                  // }}
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
              <Form.Item name="metadata" label="元数据" rules={[{ max: 2000, message: '长度超限' }]}>
                <AceEditor
                  style={{
                    width: '100%',
                    height: '25vh',
                  }}
                  mode="json"
                  theme="monokai"
                />
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
