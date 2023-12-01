import { Form, Row, Col, Modal } from 'antd'
import AceEditor from 'react-ace'
import 'ace-builds/src-noconflict/theme-monokai'
import 'ace-builds/src-noconflict/mode-json'
import { useEffect } from 'react'
import { getRole, updateRoleStatement } from '../services/wildgoods/api'

export interface RoleStatementModalProps {
  id: string
  open?: boolean
  onClose?: () => void
}

const RoleStatementModal: React.FC<RoleStatementModalProps> = (props) => {
  const [form] = Form.useForm<{
    statement: string
  }>()

  useEffect(() => {
    const init = async () => {
      form.resetFields()
      if (!props.id) {
        return
      }
      const res = await getRole(props.id)

      form.setFieldsValue({
        statement: JSON.stringify(JSON.parse(res.data.statement), null, '\t'),
      })
    }
    init()
  }, [props.id, form])

  const onOk = () => {
    form.validateFields().then(async () => {
      const values = form.getFieldsValue()

      await updateRoleStatement(props.id, values)
      if (props.onClose) {
        props.onClose()
      }
    })
  }
  return (
    <>
      <Modal
        title="编辑权限策略"
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
              <Form.Item name="statement" label="" rules={[{ max: 6000, message: '长度超限' }]}>
                <AceEditor style={{ height: 100 }} mode="json" theme="monokai" />
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Modal>
    </>
  )
}

export default RoleStatementModal
