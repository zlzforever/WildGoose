import { PageContainer } from '@ant-design/pro-layout'
import { Button, Card, Popconfirm, Select, Space, Table, Tag, message, theme, SelectProps, Modal, Breadcrumb } from 'antd'
import React, { useEffect, useState } from 'react'
import { addAssignableRole, deleteRole, getRoles, deleteAssignableRole } from '../services/wildgoods/api'
import RoleModal from '../components/RoleModal'
import RoleStatementModal from '../components/RoleStatementModal'
import { PlusOutlined, SearchOutlined } from '@ant-design/icons'
import { ObjectId } from 'bson'
import { ColumnType } from 'antd/es/table'

const baseStyle: React.CSSProperties = {
  width: '100%',
  height: '100%',
  textAlign: 'left',
}

const RolePage: React.FC = () => {
  const { token } = theme.useToken()
  const [keyword] = useState<string>('')
  const [dataSource, setDataSource] = useState<RoleDto[]>([])
  const [pagination, setPagination] = useState({
    current: 1,
    pageSize: window.wildgoods.pageSize,
    total: 0,
  })
  const [roleModalOpen, setRoleModalOpen] = useState(false)
  const [roleStatementModalOpen, setRoleStatementModalOpen] = useState(false)
  const [id, setId] = useState<string>()
  const [roleOptions, setRoleOptions] = useState<SelectProps['options']>([])
  const [selectedRoles, setSelectedRoles] = useState([])

  const tagPlusStyle: React.CSSProperties = {
    height: 22,
    background: token.colorBgContainer,
    borderStyle: 'dashed',
    cursor: 'pointer',
  }

  const columns: ColumnType<RoleDto>[] = [
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
      render: (_: string, record) => {
        if (record.name === 'admin' || record.name === 'organization-admin') {
          return <></>
        }
        record.assignableRoles = record.assignableRoles ?? []
        return (
          <>
            <Space
              size={[5, 10]}
              style={{
                flexWrap: 'wrap',
                justifyContent: 'flex-start',
              }}>
              {record.assignableRoles.map((x) => {
                return (
                  <Tag
                    key={new ObjectId().toHexString()}
                    closeIcon
                    onClose={(e) => {
                      e.preventDefault() // 阻止默认关闭行为
                      onAssignableRoleDelete(record.id, x.id)
                    }}>
                    {x.name}
                  </Tag>
                )
              })}
              <Popconfirm
                title="查找"
                description={() => {
                  return (
                    <>
                      <Select
                        style={{
                          width: 200,
                          marginTop: 10,
                          marginBottom: 10,
                        }}
                        mode="multiple"
                        showSearch
                        defaultActiveFirstOption={false}
                        filterOption={false}
                        suffixIcon={null}
                        notFoundContent={null}
                        options={(roleOptions || []).map((d) => ({
                          value: d.value,
                          label: d.label,
                        }))}
                        popupMatchSelectWidth={false}
                        value={selectedRoles}
                        onChange={(value) => {
                          setSelectedRoles(value)
                        }}
                        onSearch={onSearchRoleOptions}></Select>
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
                  setRoleOptions([])
                  setSelectedRoles([])
                  await loadRoles(keyword, pagination.pageSize, pagination.current)
                }}
                onOpenChange={(open: boolean) => {
                  if (!open) {
                    setSelectedRoles([])
                  }
                }}
                icon={<SearchOutlined />}>
                <Tag
                  style={tagPlusStyle}
                  icon={<PlusOutlined />}
                  onClick={() => {
                    onSearchRoleOptions('')
                  }}></Tag>
              </Popconfirm>
            </Space>
          </>
        )
      },
    },
    {
      title: '权限版本',
      dataIndex: 'version',
      key: 'version',
      width: 90
    },
    {
      title: '修改时间',
      dataIndex: 'lastModificationTime',
      key: 'lastModificationTime',
      width: 170,
    },
    {
      title: '操作',
      key: 'action',
      fixed: 'right',
      width: 190,
      render: (_: string, record) =>
        record.name === 'admin' || record.name === 'organization-admin' ? (
          <></>
        ) : (
          <Space
            size={0}
            style={{
              width: '100%',
              justifyContent: 'flex-end',
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
              权限
            </Button>
            <Popconfirm
              title="警告"
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

  const onSearchRoleOptions = async (newValue: string) => {
    const res = await getRoles({
      q: newValue,
      page: 1,
      limit: 10,
    })

    const result = res.data as PageData<RoleDto>
    if (result) {
      const data = result.data.map((x) => {
        return {
          value: x.id,
          label: x.name,
        }
      })
      setRoleOptions(data)
    }
    // 若返回的数据不是标准分页数， 则状态保持不变
    else {
      message.error('数据格式异常')
    }
  }

  const onAssignableRoleDelete = (id: string, assignableRoleId: string) => {
    Modal.confirm({
      title: '警告',
      content: '确定要删除此角色吗？',
      onOk() {
        deleteAssignableRole(id, assignableRoleId).then(async () => {
          message.success('删除成功')
          await loadRoles(keyword, pagination.pageSize, pagination.current)
        })
      },
    })
  }

  const onRoleDelete = async (id: string) => {
    await deleteRole(id)
    message.success('删除角色成功')
    await loadRoles(keyword, pagination.pageSize, pagination.current)
  }

  const clean = () => {
    setDataSource([])
    setPagination({
      current: 1,
      pageSize: window.wildgoods.pageSize,
      total: 0,
    })
  }

  async function loadRoles(q: string, limit: number, page: number) {
    const result = await getRoles({
      q: q,
      page,
      limit,
    })

    const data = result.data as PageData<RoleDto>
    if (data) {
      setDataSource(data.data)
      setPagination({
        total: data.total,
        pageSize: data.limit,
        current: data.page,
      })
    }
    // 若返回的数据不是标准分页数， 则状态保持不变
    else {
      message.error('数据格式异常')
    }
  }

  // const onChange = async (p: TablePaginationConfig) => {
  //   await loadRoles(keyword, p.current, p.pageSize)
  // }

  useEffect(() => {
    clean()
    loadRoles('', window.wildgoods.pageSize, 1)
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
        title={false}
        breadcrumbRender={() => {
          return (
            <Breadcrumb
              style={{
                marginTop: 10
              }}
              items={[
                {
                  title: '首页',
                },
                {
                  title: "角色管理",
                },
              ]}
            />
          ) 
        }}
      >
        {id ? (
          <RoleStatementModal
            open={roleStatementModalOpen}
            id={id}
            onOk={() => {
              setRoleStatementModalOpen(false)
            }}
            onClose={() => {
              setRoleStatementModalOpen(false)
            }}></RoleStatementModal>
        ) : (
          <></>
        )}
        <RoleModal
          open={roleModalOpen}
          onSuccess={async () => {
            setRoleModalOpen(false)
            await loadRoles(keyword, pagination.pageSize, pagination.current)
          }}
          id={id}
          onClose={() => {
            setRoleModalOpen(false)
          }}></RoleModal>
        <Card style={{ ...baseStyle }}>
          <Button onClick={onAdd}>添加</Button>
          <p />
          <Table rowKey="id" columns={columns} dataSource={dataSource} pagination={pagination} bordered size="small" scroll={{x: 'max-content'}}></Table>
        </Card>
      </PageContainer>
    </>
  )
}
export default RolePage
