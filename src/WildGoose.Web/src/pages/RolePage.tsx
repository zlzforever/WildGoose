import { PageContainer } from '@ant-design/pro-layout'
import { Button, Card, Popconfirm, Select, Space, Table, TablePaginationConfig, Tag, message, theme, SelectProps, Modal, Divider } from 'antd'
import React, { useEffect, useState } from 'react'
import { addAssignableRole, deleteRole, getRoles, deleteAssignableRole } from '../services/wildgoods/api'
import RoleModal from '../components/RoleModal'
import RoleStatementModal from '../components/RoleStatementModal'
import { PageData } from '../lib/request'
import { FireOutlined, PlusOutlined } from '@ant-design/icons'
const baseStyle: React.CSSProperties = {
  width: '100%',
  height: '100%',
  textAlign: "left",
}

const DefaultPaginiation = {
  current: 1,
  pageSize: 10,
  total: 0,
}

interface RoleBasicDto {
  id: string
  name: string
}

const RolePage: React.FC = () => {
  const { token } = theme.useToken()
  const [keyword] = useState('')
  const [dataSource, setDataSource] = useState([])
  const [pagination, setPagination] = useState(DefaultPaginiation)
  const [roleModalOpen, setRoleModalOpen] = useState(false)
  const [roleStatementModalOpen, setRoleStatementModalOpen] = useState(false)
  const [id, setId] = useState<string>()
  const [selectRoleData, setSelectRoleData] = useState<SelectProps['options']>([])
  const [selectedRoles, setSelectedRoles] = useState([])

  const tagPlusStyle: React.CSSProperties = {
    height: 22,
    background: token.colorBgContainer,
    borderStyle: 'dashed',
  }

  const columns = [
    {
      title: '名称',
      dataIndex: 'name',
      key: 'name',
    },
    {
      title: '备注',
      dataIndex: 'description',
      key: 'description',
    },
    {
      title: '可授于角色',
      dataIndex: 'assignableRoles',
      key: 'assignableRoles',
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      render: (_: any, record: any) => {
        if (record.name === 'admin') {
          return <></>
        }
        record.assignableRoles = record.assignableRoles ?? []
        return (
          <>
            {record.assignableRoles.map((x: RoleBasicDto) => {
              return (
                <Tag
                  closeIcon
                  // eslint-disable-next-line @typescript-eslint/no-explicit-any
                  onClose={(e: any) => {
                    e.preventDefault() // 阻止默认关闭行为
                    showConfirmDeleteAssignableRole(record.id, x.id)
                  }}>
                  {x.name}
                </Tag>
              )
            })}
            <Popconfirm
              title="搜索角色"
              description={() => {
                return (
                  <>
                    <Select
                      style={{
                        width: '140px',
                      }}
                      mode="multiple"
                      showSearch
                      defaultActiveFirstOption={false}
                      filterOption={false}
                      suffixIcon={null}
                      notFoundContent={null}
                      // eslint-disable-next-line @typescript-eslint/no-explicit-any
                      options={(selectRoleData || []).map((d: any) => ({
                        value: d.value,
                        label: d.label,
                      }))}
                      value={selectedRoles}
                      onChange={(value) => {
                        setSelectedRoles(value)
                      }}
                      onSearch={handleSearch}></Select>
                  </>
                )
              }}
              onConfirm={async () => {
                const command = selectedRoles.map((x) => {
                  return {
                    id: record.id,
                    assignableRoleId: x,
                  }
                })
                await addAssignableRole(command)
                message.success('修改成功')
                setSelectRoleData([])
                setSelectedRoles([])
                await loadData(keyword, pagination.current, pagination.pageSize)
              }}
              icon={<FireOutlined />}>
              <Tag style={tagPlusStyle} icon={<PlusOutlined />}></Tag>
            </Popconfirm>
          </>
        )
      },
    },
    {
      title: '策略版本',
      dataIndex: 'version',
      key: 'version',
    },
    {
      title: '修改时间',
      dataIndex: 'lastModificationTime',
      key: 'lastModificationTime',
    },
    {
      title: '操作',
      key: 'action',
      width: 120,
      render: (_: any, record: any) =>
        record.name === 'admin' ? (
          <></>
        ) : (
          <Space size="middle" style={{
            width:"100%",
            justifyContent:"flex-end"
          }}>
            <Button
              type="link"
              onClick={() => {
                setId(record.id)
                setRoleModalOpen(true)
              }}>
              编辑
            </Button>
            <Button
              type="link"
              onClick={() => {
                setId(record.id)
                setRoleStatementModalOpen(true)
              }}>
              权限策略
            </Button>
            <Popconfirm
              title="删除角色"
              description="您确定要删除此角色吗?"
              onConfirm={() => {
                onRoleDelete(record.id)
              }}
              okText="确定"
              cancelText="取消">
              <Button type="link">删除</Button>
            </Popconfirm>
          </Space>
        ),
    },
  ]

  const handleSearch = async (newValue: string) => {
    if (!newValue) {
      setSelectRoleData([])
      return
    }
    const res = await getRoles({
      q: newValue,
      page: 1,
      limit: 10,
    })
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const result = res.data as any
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const data = result.data.map((x: any) => {
      return {
        value: x.id,
        label: x.name,
      }
    })
    setSelectRoleData(data)
  }

  const showConfirmDeleteAssignableRole = (id: string, assignableRoleId: string) => {
    Modal.confirm({
      title: '警告',
      content: '确认要删除这个角色吗？',
      onOk() {
        deleteAssignableRole(id, assignableRoleId).then(async () => {
          await loadData(keyword, pagination.current, pagination.pageSize)
          message.success('删除成功')
        })
      },
      onCancel() {},
    })
  }

  const onRoleDelete = async (id: string) => {
    await deleteRole(id)
    await loadData(keyword, pagination.current, pagination.pageSize)
    message.success('删除角色成功')
  }

  const clean = () => {
    setDataSource([])
    setPagination(DefaultPaginiation)
  }

  async function loadData(q: string, page: number | undefined, limit: number | undefined) {
    const result = await getRoles({
      q: q,
      page,
      limit,
    })

    const data = result.data as PageData
    if (data) {
      setDataSource(data.data)
      setPagination({
        total: data.total,
        pageSize: data.limit,
        current: data.page,
      })
    }
  }

  const onChange = async (p: TablePaginationConfig) => {
    await loadData(keyword, p.current, p.pageSize)
  }

  useEffect(() => {
    clean()
    loadData('', 1, 10)
  }, [])

  const onAdd = () => {
    setId('')
    setRoleModalOpen(true)
  }

  return (
    <>
      <PageContainer
        token={{
          paddingInlinePageContainerContent: 20,
        }}
        title={false}>
        {id ? (
          <RoleStatementModal
            open={roleStatementModalOpen}
            id={id}
            onClose={() => {
              setRoleStatementModalOpen(false)
            }}></RoleStatementModal>
        ) : (
          <></>
        )}
        <RoleModal
          open={roleModalOpen}
          onSuccess={async () => {
            await loadData(keyword, pagination.current, pagination.pageSize)
          }}
          id={id}
          onClose={() => {
            setRoleModalOpen(false)
          }}></RoleModal>
        <Card style={{ ...baseStyle }}>
          <Button onClick={onAdd}>添加</Button>
          <Divider></Divider>
          <Table rowKey="id" columns={columns} dataSource={dataSource} pagination={pagination} onChange={onChange}
            bordered
          ></Table>
        </Card>
      </PageContainer>
    </>
  )
}
export default RolePage
