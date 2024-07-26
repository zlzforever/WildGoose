import { Form, Row, Col, Input, Modal } from "antd"
import { useEffect } from "react"
import { addRole, getRole, updateRole } from "../services/wildgoose/api"

export interface RoleModalProps {
  id?: string
  open?: boolean
  onClose?: () => void
  onSuccess?: () => void
}
const { TextArea } = Input

const RoleModal: React.FC<RoleModalProps> = (props) => {
  const [form] = Form.useForm<{
    name: string
    description: string
  }>()
  const title = props.id ? "编辑" : "添加角色"

  useEffect(() => {
    const init = async () => {
      if (!props.open) {
        return
      }
      form.resetFields()

      if (!props.id) {
        return
      }
      const res = await getRole(props.id)
      form.setFieldsValue(res.data)
    }
    init()
  }, [props.id, props.open, form])

  const onOk = () => {
    form.validateFields().then(async () => {
      const values = form.getFieldsValue()
      if (props.id) {
        await updateRole(props.id, values)
      } else {
        await addRole(values)
      }

      if (props.onSuccess) {
        props.onSuccess()
      }
    })
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
          form && form.resetFields()
          if (props.onClose) {
            props.onClose()
          }
        }}
      >
        <Form layout="vertical" form={form}>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item
                name="name"
                label="名称"
                rules={[{ required: true, message: "请输入名称" }]}
              >
                <Input placeholder="请输入名称" maxLength={100} />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="description" label="备注">
                <TextArea style={{ height: 100 }} placeholder="请输入备注" maxLength={512} />
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

export default RoleModal
