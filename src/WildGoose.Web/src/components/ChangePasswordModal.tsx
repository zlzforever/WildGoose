import { Modal, Form, Row, Col, Input, message } from 'antd'
import { changePassword } from '../services/wildgoods/api'
import { useEffect } from 'react'

export interface ChangePasswordlProps {
  id?: string
  open?: boolean
  onClose?: () => void
}

const ChangePasswordModal: React.FC<ChangePasswordlProps> = (props) => {
  const [form] = Form.useForm<{
    newPassword: string
    confirmPassword: string
  }>()

  useEffect(() => {
    form.resetFields()
  }, [form, props.id])

  const onOk = async () => {
    if (!props.id) {
      return
    }
    await form.validateFields()
    const values = form.getFieldsValue()
    await changePassword(props.id, values)
    message.success('操作成功')
    form.resetFields()

    if (props.onClose) {
      props.onClose()
    }
  }

  return (
    <>
      <Modal
        title={'修改密码'}
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
              <Form.Item name="newPassword" label="新密码" rules={[{ required: true, message: '请输入新密码' }]}>
                <Input placeholder="请输入新密码" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={16}>
            <Col span={24}>
              <Form.Item name="confirmPassword" label="重复密码" rules={[{ required: true, message: '请输入重复密码' }]}>
                <Input placeholder="请输入重复密码" />
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

export default ChangePasswordModal
