import { PageContainer } from '@ant-design/pro-layout'
import { Button, Card, Flex, Input, Menu, MenuProps, Modal, Popconfirm, Select, Table, Tag, Tooltip, Tree, message } from 'antd'
import { getSubOrganizationList, deleteOrganization, getUsers, deleteUser, enableUser, disableUser } from '../services/wildgoods/api'
import { useEffect, useState } from 'react'
import OrganizationModal from '../components/OrganizationModal'
import { AppstoreOutlined, MoreOutlined, SmileOutlined } from '@ant-design/icons'
import UserModal from '../components/UserModal'
import { PageData } from '../lib/request'
import ChangePasswordModal from '../components/ChangePasswordModal'
import { ObjectId } from 'bson'

const { Search } = Input

const DefaultPaginiation = {
  current: 1,
  pageSize: 10,
  total: 0,
}
type MenuItem = Required<MenuProps>['items'][number]

const UserPage = () => {
  const [keyword] = useState('')
  const [dataSource, setDataSource] = useState([])
  const [pagination, setPagination] = useState(DefaultPaginiation)

  const [userModalOpen, setUserModalOpen] = useState(false)
  const [user, setUser] = useState<{
    id: string
    organization?: {
      id: string
      pId: string
      value: string
      title: string
      isLeaf: boolean
    }
  }>({
    id: '',
    organization: undefined,
  })
  const [userSelectedKeys, setUserSelectedKeys] = useState<string[]>()

  const [organizationTreeData, setOrganizationTreeData] = useState<{ key: string; pId: string; title: string; isLeaf: boolean; children: [] }[]>([])
  // eslint-disable-next-line react-hooks/exhaustive-deps, @typescript-eslint/no-explicit-any
  const [organizationTreeDict, setOrganizationTreeDict] = useState({} as any)
  const [organizationModalOpen, setOrganizationModalOpen] = useState(false)
  const [organizationTreeSelectedKeys, setOrganizationTreeSelectedKeys] = useState<string[]>([])
  const [organizationTreeExpandedKeys, setOrganizationTreeExpandedKeys] = useState<string[]>([])
  const [organizationModalParams, setOrganizationModalParams] = useState<{
    id: string
    parentId: string
    parentName: string
  }>({
    id: '',
    parentId: '',
    parentName: '',
  })

  const [changePasswordModalOpen, setChangePasswordModalOpen] = useState(false)

  const columns = [
    {
      title: '姓名',
      dataIndex: 'name',
      key: 'name',
    },
    {
      title: '帐号',
      dataIndex: 'userName',
      key: 'userName',
    },
    {
      title: '电话',
      dataIndex: 'phoneNumber',
      key: 'phoneNumber',
    },
    {
      title: '启用',
      dataIndex: 'enabled',
      key: 'enabled',
      render: (_: never, record: { enabled: boolean }) => {
        if (record.enabled) {
          return (
            <>
              <Tag key={new ObjectId().toHexString()} color="success">
                启用
              </Tag>
            </>
          )
        } else {
          return (
            <>
              <Tag key={new ObjectId().toHexString()} color="error">
                禁用
              </Tag>
            </>
          )
        }
      },
    },
    {
      title: '角色',
      dataIndex: 'roles',
      key: 'roles',
      render: (_: never, record: { roles: string[] }) => {
        if (record.roles) {
          return (
            <>
              {record.roles.map((x) => {
                return <Tag key={new ObjectId().toHexString()}>{x}</Tag>
              })}
            </>
          )
        } else {
          return <></>
        }
      },
    },
    {
      title: '所在机构',
      dataIndex: 'organizations',
      key: 'organizations',
      render: (_: never, record: { organizations: string[] }) => {
        if (record.organizations) {
          return (
            <>
              {record.organizations.map((x) => {
                return <Tag key={new ObjectId().toHexString()}>{x}</Tag>
              })}
            </>
          )
        } else {
          return <></>
        }
      },
    },
    {
      title: '创建时间',
      dataIndex: 'creationTime',
      key: 'creationTime',
    },
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
  ] as any

  const clean = () => {
    setDataSource([])
    setPagination(DefaultPaginiation)
  }

  useEffect(() => {
    clean()
    // loadData('', 'all', 1, 15)
    const init = async () => {
      const res = await getSubOrganizationList()
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const result = res.data as { id: string }[]
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const dict = {} as any
      if (result && result.length > 0) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const data = result.map((x: any) => {
          const n = {
            key: x.id,
            title: x.name,
            pId: x.parentId,
            isLeaf: !x.hasChild,
            children: [],
          }
          dict[x.id] = n
          return n
        }) as never[]
        setOrganizationTreeDict(dict)
        setOrganizationTreeData(data)
        setOrganizationTreeSelectedKeys([result[0].id])
      } else {
        setOrganizationTreeData([])
        setOrganizationTreeSelectedKeys([])
      }
    }
    init()
  }, [])

  // async function loadData(q: string, status: string, page: number | undefined, limit: number | undefined) {
  //   const result = await getUsers({
  //     q: q,
  //     status: status,
  //     page,
  //     limit,
  //   })
  //   const data = result.data as PageData
  //   if (data) {
  //     setDataSource(data.data)
  //     setPagination({
  //       total: data.total,
  //       pageSize: data.limit,
  //       current: data.page,
  //     })
  //   }
  // }

  useEffect(() => {
    if (organizationTreeSelectedKeys && organizationTreeSelectedKeys.length > 0) {
      loadUsers(organizationTreeSelectedKeys[0], keyword, 'all', DefaultPaginiation.pageSize, DefaultPaginiation.current)
    } else {
      setDataSource([])
    }
    setUserSelectedKeys([])
  }, [organizationTreeSelectedKeys])

  const loadUsers = async (orgId: string, q: string, status: string, limit: number | undefined, page: number | undefined) => {
    if (!orgId) {
      return
    }
    const res = await getUsers({
      organizationId: orgId,
      q: q,
      limit: limit,
      status: status,
      page: page,
    })
    const result = res.data as PageData
    if (result) {
      setDataSource(result.data)
      setPagination({
        pageSize: result.limit,
        current: result.page,
        total: result.total,
      })
    }
  }

  // // eslint-disable-next-line @typescript-eslint/no-explicit-any
  // function recursiveSearch(node: { key: string; children: [] }, id: string): any {
  //   if (node.key === id) {
  //     return node
  //   }
  //   if (node.children) {
  //     for (let i = 0; i < node.children.length; i++) {
  //       const result = recursiveSearch(node.children[i], id)
  //       if (result) {
  //         return result
  //       }
  //     }
  //   }
  //   return null
  // }

  // // eslint-disable-next-line @typescript-eslint/no-explicit-any
  // const findOrganization = (id: string): any => {
  //   if (!id) {
  //     return null
  //   }
  //   for (let i = 0; i < organizationTreeData.length; ++i) {
  //     // eslint-disable-next-line @typescript-eslint/no-explicit-any
  //     const node = organizationTreeData[i] as any
  //     if (node.key === id) {
  //       return node
  //     }
  //     const result = recursiveSearch(node, id)
  //     if (result) {
  //       return result
  //     }
  //   }
  //   return null
  // }

  const onOrganizationTreeLoadData = async (node: { key: string; children: [] }) => {
    if (node.children.length > 0) {
      return
    }
    const res = await getSubOrganizationList(node.key)
    const subOrganizations = res.data as []
    if (subOrganizations && subOrganizations.length > 0) {
      const data = subOrganizations.map((x: { id: string; parentId: string; name: string; hasChild: boolean }) => {
        const n = {
          key: x.id,
          title: x.name,
          pId: x.parentId,
          isLeaf: !x.hasChild,
          children: [],
        }
        organizationTreeDict[x.id] = n
        return n
      })
      const parent = organizationTreeDict[node.key]
      if (parent) {
        parent.children = data
      }
      setOrganizationTreeDict(organizationTreeDict)
      setOrganizationTreeData([...organizationTreeData])
    }
  }

  const onOrganizationModalOk = async (values: { id: string; name: string; parentId: string; hasChild: boolean }, originParentId?: string) => {
    // 添加机构
    if (!organizationModalParams.id) {
      onOrganizationAdd(values)
    }
    // 编辑机构
    else {
      onOrganizationUpdate(values, originParentId)
    }
  }

  const onOrganizationAdd = (values: { id: string; name: string; parentId: string; hasChild: boolean }) => {
    const organization = {
      key: values.id,
      title: values.name,
      pId: values.parentId,
      isLeaf: !values.hasChild,
      children: [],
    }

    // 添加了根机构
    if (!organization.pId) {
      organizationTreeDict[organization.key] = organization
      setOrganizationTreeData(organizationTreeData.concat(organization as never))
      setOrganizationTreeDict(organizationTreeDict)
    } else {
      const parent = organizationTreeDict[organization.pId]
      parent.isLeaf = false
      // 只有父节点已经被展开， 才进行节点添加操作
      if (organizationTreeExpandedKeys.indexOf(organization.pId) >= 0) {
        parent.children = parent.children.concat(organization)
      }
      setOrganizationTreeData([...organizationTreeData])
    }
  }

  const onOrganizationUpdate = async (values: { id: string; name: string; parentId: string; hasChild: boolean }, originParentId?: string) => {
    const key = values.id
    const pId = values.parentId
    const organization = organizationTreeDict[key]
    organization.title = values.name
    organization.pId = pId

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    let data = [] as any[]
    // 原始为根级机构
    if (!originParentId) {
      // 依然为根级机构
      if (!pId) {
        // 不需要额外操作
        data = organizationTreeData
      }
      // 已经变化为非根机构
      else {
        // 从根一级中移除
        data = organizationTreeData.filter((item) => item.key !== key)
        const parent = organizationTreeDict[pId]
        parent.isLeaf = false
        // 添加至新机构（仅在其上级机构已经被展开的情况下操作）
        if (organizationTreeExpandedKeys.indexOf(organization.pId) >= 0) {
          parent.children.push(organization)
        }
      }
    }
    // 原始不为根级节点
    else {
      // 上级机构无变化
      if (originParentId === pId) {
        // 不需要额外操作
        data = organizationTreeData
      } else {
        // 此时， 上级机构一定是已经被展开了， 不然无法被编辑到
        // 从原上级机构删除
        const originParent = organizationTreeDict[originParentId]
        originParent.children = originParent.children.filter((item: { key: string }) => item.key !== key)
        originParent.isLeaf = originParent.children.length === 0

        // 添加至新机构
        if (pId) {
          const parent = organizationTreeDict[pId]
          parent.isLeaf = false

          // 添加至新机构（仅在其上级机构已经被展开的情况下操作）
          if (organizationTreeExpandedKeys.indexOf(organization.pId) >= 0) {
            parent.children.push(organization)
          }
        }
        // 移动为根机构
        else {
          data = organizationTreeData.concat(organization)
        }
      }
    }

    setOrganizationTreeData([...data])
  }

  const onOrganizationDelete = async (item: { pId: string; key: string }) => {
    await deleteOrganization(item.key)
    if (item.pId) {
      const parent = organizationTreeDict[item.pId]
      parent.children = parent.children.filter((y: { key: string }) => y.key !== item.key)
      parent.isLeaf = parent.children.length === 0
      setOrganizationTreeData([...organizationTreeData])
    } else {
      setOrganizationTreeData((origin) => origin.filter((y: { key: string }) => y.key !== item.key))
    }
  }

  const onOrganizationModalClose = () => {
    setOrganizationModalParams({
      id: '',
      parentId: '',
      parentName: '',
    })
    setOrganizationModalOpen(false)
  }

  const organizationTreeTitleRender = (node: { key: string; pId: string; title: string; isLeaf: boolean }) => {
    const items: MenuItem[] = [
      {
        label: '编辑机构',
        key: 'a',
        icon: <AppstoreOutlined />,
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        onClick: () => {
          setOrganizationModalParams({
            id: node.key,
            parentId: '',
            parentName: '',
          })
          setOrganizationModalOpen(true)
        },
      },
      {
        label: '添加下级机构',
        key: 'b',
        icon: <AppstoreOutlined />,
        onClick: () => {
          setOrganizationModalParams({
            id: '',
            parentId: node.key,
            parentName: node.title,
          })
          setOrganizationModalOpen(true)
        },
      },
      {
        label: '删除',
        key: 'c',
        icon: <AppstoreOutlined />,
        onClick: () => {
          Modal.confirm({
            title: '警告',
            content: '确认要删除这个机构吗？',
            onOk() {
              onOrganizationDelete(node)
            },
          })
        },
      },
    ]

    return (
      <>
        {node.title}
        <Tooltip
          trigger="click"
          color="#ffffff"
          key={node.key + '_tooltip'}
          arrow={false}
          title={() => {
            return (
              <>
                <Menu mode="inline" style={{ borderInlineEnd: 0 }} items={items} />
              </>
            )
          }}>
          <MoreOutlined />
        </Tooltip>
      </>
    )
  }

  const onUserDelete = async () => {
    if (userSelectedKeys && userSelectedKeys.length > 0) {
      const key = userSelectedKeys[0]
      await deleteUser(key)
      message.success('操作成功')
      await loadUsers(organizationTreeSelectedKeys[0], keyword, 'all', pagination.pageSize, pagination.current)
    }
  }

  const onUserEnable = async () => {
    if (userSelectedKeys && userSelectedKeys.length > 0) {
      const key = userSelectedKeys[0]
      await enableUser(key)
      message.success('操作成功')
      await loadUsers(organizationTreeSelectedKeys[0], keyword, 'all', pagination.pageSize, pagination.current)
    }
  }

  const onUserDisable = async () => {
    if (userSelectedKeys && userSelectedKeys.length > 0) {
      const key = userSelectedKeys[0]
      await disableUser(key)
      message.success('操作成功')
      await loadUsers(organizationTreeSelectedKeys[0], keyword, 'all', pagination.pageSize, pagination.current)
    }
  }
  return (
    <>
      <PageContainer
        token={{
          paddingInlinePageContainerContent: 40,
        }}
        title="用户管理">
        <ChangePasswordModal
          id={userSelectedKeys && userSelectedKeys.length === 1 ? userSelectedKeys[0] : ''}
          open={changePasswordModalOpen}
          onClose={() => {
            setChangePasswordModalOpen(false)
          }}></ChangePasswordModal>
        <UserModal
          user={user}
          open={userModalOpen}
          onClose={async () => {
            await loadUsers(organizationTreeSelectedKeys[0], keyword, 'all', pagination.pageSize, pagination.current)
            setUserModalOpen(false)
          }}></UserModal>
        <OrganizationModal
          open={organizationModalOpen}
          id={organizationModalParams.id}
          parent={{
            id: organizationModalParams.parentId,
            name: organizationModalParams.parentName,
          }}
          onClose={onOrganizationModalClose}
          onOk={onOrganizationModalOk}></OrganizationModal>
        <Flex gap="middle">
          <Card>
            <Flex vertical>
              <Search placeholder="请输入机构名称" allowClear style={{ width: 200 }} />
              <Tree
                showLine
                icon={<SmileOutlined />}
                showIcon={true}
                treeData={organizationTreeData}
                loadData={onOrganizationTreeLoadData}
                expandedKeys={organizationTreeExpandedKeys}
                titleRender={organizationTreeTitleRender}
                selectedKeys={organizationTreeSelectedKeys}
                onSelect={(
                  // eslint-disable-next-line @typescript-eslint/no-explicit-any
                  key: any
                ) => {
                  setOrganizationTreeSelectedKeys(key)
                }}
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                onExpand={(v: any) => {
                  setOrganizationTreeExpandedKeys(v)
                }}></Tree>
              <Button
                onClick={() => {
                  setOrganizationModalParams({
                    id: '',
                    parentId: '',
                    parentName: '',
                  })
                  setOrganizationModalOpen(true)
                }}>
                添加机构
              </Button>
            </Flex>
          </Card>
          <Card style={{ width: '100%' }}>
            <Flex gap="middle" align="start" vertical>
              <Flex align="start">
                <Select
                  defaultValue="all"
                  style={{ width: 120 }}
                  options={[
                    { value: 'all', label: '全部' },
                    { value: 'inactive', label: '未激活' },
                    { value: 'disabled', label: '已暂停' },
                  ]}
                />
                <Search placeholder="请输入用户名、手机号" allowClear style={{ width: 200 }} onClick={() => {}} />
                <Flex justify="flex-end" align="flex-end">
                  <Button
                    type="primary"
                    onClick={() => {
                      if (organizationTreeSelectedKeys && organizationTreeSelectedKeys.length > 0) {
                        const key = organizationTreeSelectedKeys[0]
                        const organization = organizationTreeDict[key]
                        setUser({
                          id: '',
                          organization: {
                            id: organization.key,
                            pId: organization.pId,
                            value: organization.key,
                            title: organization.title,
                            isLeaf: organization.isLeaf,
                          },
                        })
                      } else {
                        setUser({
                          id: '',
                          organization: undefined,
                        })
                      }

                      setUserModalOpen(true)
                    }}>
                    添加
                  </Button>
                  <Button
                    disabled={!(userSelectedKeys && userSelectedKeys.length === 1)}
                    type="primary"
                    onClick={() => {
                      if (userSelectedKeys && userSelectedKeys.length > 0) {
                        setUser({
                          id: userSelectedKeys[0],
                          organization: undefined,
                        })
                        setUserModalOpen(true)
                      }
                    }}>
                    查看
                  </Button>
                  <Popconfirm
                    title="警告"
                    description="您确定要删除此用户吗?"
                    onConfirm={() => {
                      onUserDelete()
                    }}
                    okText="确定"
                    cancelText="取消">
                    <Button disabled={!(userSelectedKeys && userSelectedKeys.length === 1)} type="primary">
                      删除
                    </Button>
                  </Popconfirm>
                  <Button
                    disabled={!(userSelectedKeys && userSelectedKeys.length === 1)}
                    type="primary"
                    onClick={() => {
                      onUserEnable()
                    }}>
                    启用
                  </Button>
                  <Popconfirm
                    title="警告"
                    description="您确定要禁用用户吗?"
                    onConfirm={() => {
                      onUserDisable()
                    }}
                    okText="确定"
                    cancelText="取消">
                    <Button disabled={!(userSelectedKeys && userSelectedKeys.length === 1)} type="primary">
                      禁用
                    </Button>
                  </Popconfirm>
                  <Button
                    type="primary"
                    disabled={!(userSelectedKeys && userSelectedKeys.length === 1)}
                    onClick={() => {
                      setChangePasswordModalOpen(true)
                    }}>
                    修改密码
                  </Button>
                  <Button type="primary">导出</Button>
                  <Button type="primary">导入</Button>
                </Flex>
              </Flex>
              <Table
                rowKey="id"
                columns={columns}
                dataSource={dataSource}
                pagination={pagination}
                rowSelection={{
                  type: 'checkbox',
                  selectedRowKeys: userSelectedKeys,
                  onChange: (selectedRowKeys: React.Key[]) => {
                    setUserSelectedKeys(selectedRowKeys as string[])
                  },
                }}
                style={{ width: '100%' }}></Table>
            </Flex>
          </Card>
        </Flex>
      </PageContainer>
    </>
  )
}

export default UserPage
