import { PageContainer } from '@ant-design/pro-layout'
import { Button, Card, Flex, Input, Menu, MenuProps, Modal, Popconfirm, Select, Switch, Table, Tag, Tooltip, Tree, message } from 'antd'
import { getSubOrganizationList, deleteOrganization, getUsers, deleteUser, enableUser, disableUser, addOrganizationAdministrator, deleteOrganizationAdministrator } from '../services/wildgoods/api'
import { Key, useEffect, useState } from 'react'
import OrganizationModal from '../components/OrganizationModal'
import { AppstoreOutlined, MoreOutlined, SmileOutlined } from '@ant-design/icons'
import UserModal from '../components/UserModal'
import ChangePasswordModal from '../components/ChangePasswordModal'
import { ObjectId } from 'bson'
import { EventDataNode } from 'antd/es/tree'
import { ColumnType } from 'antd/es/table'

const { Search } = Input

type MenuItem = Required<MenuProps>['items'][number]

const UserPage = () => {
  const [keyword, setKeyword] = useState('')
  const [status, setStatus] = useState('all')
  const [dataSource, setDataSource] = useState<UserDto[]>([])
  const [pagination, setPagination] = useState({
    current: 1,
    pageSize: window.wildgoods.pageSize,
    total: 0,
  })

  const [userModalOpen, setUserModalOpen] = useState(false)
  const [userProps, setUserProps] = useState<{
    id?: string
    organization?: OrganizationDto
  }>()

  const [userSelectedKeys, setUserSelectedKeys] = useState<string[]>([])
  const [userSelected, setUserSelected] = useState<UserDto>()

  const [organizationTreeData, setOrganizationTreeData] = useState<SimpleDataNode[]>([])
  const [organizationTreeDict, setOrganizationTreeDict] = useState<Dictionary<SimpleDataNode>>({})
  const [organizationModalOpen, setOrganizationModalOpen] = useState(false)
  const [organizationTreeSelectedKeys, setOrganizationTreeSelectedKeys] = useState<string[]>([])
  const [organizationTreeExpandedKeys, setOrganizationTreeExpandedKeys] = useState<string[]>([])
  const [organizationModalParams, setOrganizationModalParams] = useState<{
    id: string
    parent: SimpleDataNode | undefined
  }>()

  const [changePasswordModalOpen, setChangePasswordModalOpen] = useState(false)

  const columns: ColumnType<UserDto>[] = [
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
      title: '管理员',
      dataIndex: 'isAdministrator',
      key: 'isAdministrator',
      render: (_: unknown, record) => {
        return (
          <>
            <Switch
              checked={record.isAdministrator}
              onChange={async () => {
                if (organizationTreeSelectedKeys && organizationTreeSelectedKeys.length > 0) {
                  if (record.isAdministrator) {
                    Modal.confirm({
                      title: '警告',
                      content: '您确定要移除此管理员吗?',
                      onOk: async () => {
                        await deleteOrganizationAdministrator(organizationTreeSelectedKeys[0], record.id)
                        record.isAdministrator = !record.isAdministrator
                        setDataSource([...dataSource])
                      },
                    })
                  } else {
                    await addOrganizationAdministrator(organizationTreeSelectedKeys[0], record.id)
                    record.isAdministrator = !record.isAdministrator
                    setDataSource([...dataSource])
                  }
                }
              }}
            />
          </>
        )
      },
    },
    {
      title: '启用',
      dataIndex: 'enabled',
      key: 'enabled',
      render: (_: unknown, record) => {
        return (
          <>
            <Switch
              checked={record.enabled}
              onChange={async () => {
                if (record.enabled) {
                  Modal.confirm({
                    title: '警告',
                    content: '您确定要禁用此用户吗?',
                    onOk: async () => {
                      await disableUser(record.id)
                      record.enabled = !record.enabled
                      setDataSource([...dataSource])
                    },
                  })
                } else {
                  record.enabled = !record.enabled
                  await enableUser(record.id)
                  setDataSource([...dataSource])
                }
              }}
            />
          </>
        )
      },
    },
    {
      title: '角色',
      dataIndex: 'roles',
      key: 'roles',
      render: (_: unknown, record) => {
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
      render: (_: unknown, record: { organizations: string[] }) => {
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
  ]

  const clean = () => {
    setKeyword('')
    setDataSource([])
    setPagination({
      current: 1,
      pageSize: window.wildgoods.pageSize,
      total: 0,
    })
  }

  useEffect(() => {
    clean()
    const init = async () => {
      const dict: Dictionary<SimpleDataNode> = {}
      const res = await getSubOrganizationList()
      const organizations = res.data as OrganizationDto[]
      if (organizations && organizations.length > 0) {
        const data = organizations.map((x) => {
          const node: SimpleDataNode = {
            key: x.id,
            title: x.name,
            pId: x.parentId,
            isLeaf: !x.hasChild,
            children: [],
          }
          dict[x.id] = node
          return node
        })
        setOrganizationTreeData(data)
        setOrganizationTreeSelectedKeys([organizations[0].id])
      } else {
        setOrganizationTreeData([])
        setOrganizationTreeSelectedKeys([])
      }
      setOrganizationTreeDict(dict)
    }
    init()
  }, [])

  const loadUsers = async (orgId: string, q: string, status: string, limit: number, page: number) => {
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
    const result = res.data as PageData<UserDto>
    // 若正常返回分页数据
    if (result) {
      setDataSource(result.data)

      setUserSelectedKeys([])
      setUserSelected(undefined)
      setPagination({
        pageSize: result.limit,
        current: result.page,
        total: result.total,
      })
    }
    // 若返回的数据不是标准分页数， 则状态保持不变
    else {
      message.error('数据格式异常')
    }
  }

  const onOrganizationTreeLoadData = async (node: EventDataNode<SimpleDataNode>) => {
    // 如果 children 已经有值， 则不再需要请求新数据
    if (node.children.length > 0 || !node.key) {
      return
    }
    const key = node.key
    const res = await getSubOrganizationList(key)
    const subOrganizations = (res.data as OrganizationDto[]) ?? []
    const data = subOrganizations.map((x) => {
      const node: SimpleDataNode = {
        key: x.id,
        title: x.name,
        pId: x.parentId,
        isLeaf: !x.hasChild,
        children: [],
      }
      organizationTreeDict[x.id] = node
      return node
    })
    if (data.length === 0) {
      return
    }
    const parent = organizationTreeDict[key]
    if (parent) {
      parent.children = data
      setOrganizationTreeData([...organizationTreeData])
    }
    setOrganizationTreeDict(organizationTreeDict)
  }

  const onOrganizationSelect = async (keys: Key[]) => {
    if (keys.length === 0) {
      return
    }
    setKeyword('')
    setStatus('all')
    if (keys.length > 0) {
      const key = keys[0] as string
      setOrganizationTreeSelectedKeys(keys as string[])
      loadUsers(key, '', 'all', window.wildgoods.pageSize, 1)
    } else {
      setDataSource([])
      setUserSelectedKeys([])
      setUserSelected(undefined)
      setOrganizationTreeSelectedKeys([])
    }
  }

  const onOrganizationModalOk = async (values: OrganizationDto, originParentId?: string) => {
    // 添加机构
    if (!organizationModalParams?.id) {
      onOrganizationAdd(values)
    }
    // 编辑机构
    else {
      onOrganizationUpdate(values, originParentId)
    }
    setOrganizationModalOpen(false)
  }

  const onOrganizationAdd = (values: OrganizationDto) => {
    const organization: SimpleDataNode = {
      key: values.id,
      title: values.name,
      pId: values.parentId,
      isLeaf: !values.hasChild,
      children: [],
    }

    // 新添加了根机构
    if (!organization.pId) {
      organizationTreeDict[values.id] = organization
      setOrganizationTreeData(organizationTreeData.concat(organization))
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

  const onOrganizationUpdate = async (values: OrganizationDto, originParentId?: string) => {
    const key = values.id
    const pId = values.parentId
    const organization = organizationTreeDict[key]
    organization.title = values.name
    organization.pId = pId

    let data: SimpleDataNode[] = []

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
        originParent.children = originParent.children.filter((item) => item.key !== key)
        originParent.isLeaf = originParent.children.length === 0

        // 添加至新机构
        if (pId) {
          const parent = organizationTreeDict[pId]
          parent.isLeaf = false

          // 添加至新机构（仅在其上级机构已经被展开的情况下操作）
          if (organizationTreeExpandedKeys.indexOf(organization.pId) >= 0) {
            parent.children.push(organization)
          }
          data = organizationTreeData
        }
        // 移动为根机构
        else {
          data = organizationTreeData.concat([organization])
        }
      }
    }

    setOrganizationTreeData([...data])
  }

  const onOrganizationDelete = async (item: SimpleDataNode) => {
    await deleteOrganization(item.key)
    if (item.pId) {
      const parent = organizationTreeDict[item.pId]
      parent.children = parent.children.filter((y) => y.key !== item.key)
      parent.isLeaf = parent.children.length === 0
      setOrganizationTreeData([...organizationTreeData])
    } else {
      setOrganizationTreeData((origin) => origin.filter((y) => y.key !== item.key))
    }
  }

  const onOrganizationModalClose = () => {
    setOrganizationModalParams(undefined)
    setOrganizationModalOpen(false)
  }

  const organizationTreeTitleRender = (node: SimpleDataNode) => {
    const items: MenuItem[] = [
      {
        label: '编辑机构',
        key: 'a',
        icon: <AppstoreOutlined />,
        onClick: () => {
          if (organizationTreeSelectedKeys.length === 0) {
            message.error('未选中机构')
            return
          }
          setOrganizationModalParams({
            id: organizationTreeSelectedKeys[0],
            parent: undefined,
          })
          setOrganizationModalOpen(true)
        },
      },
      {
        label: '添加下级机构',
        key: 'b',
        icon: <AppstoreOutlined />,
        onClick: () => {
          if (organizationTreeSelectedKeys.length === 0) {
            message.error('未选中机构')
            return
          }

          const org = organizationTreeDict[organizationTreeSelectedKeys[0]]
          setOrganizationModalParams({
            id: '',
            parent: org,
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
      await loadUsers(organizationTreeSelectedKeys[0], keyword, status, pagination.pageSize, pagination.current)
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
          id={userProps?.id}
          organization={userProps?.organization}
          open={userModalOpen}
          onOk={async (user: UserDto) => {
            setUserModalOpen(false)

            const record = dataSource.find((item) => item.id === user.id)
            if (record) {
              record.creationTime = user.creationTime
              record.enabled = user.enabled
              record.name = user.name
              record.organizations = user.organizations
              record.phoneNumber = user.phoneNumber
              record.roles = user.roles
              record.userName = user.userName
              // record.isAdministrator = user.isAdministrator 是否管理员不会在编辑页面修改
            }
            setDataSource([...dataSource])
          }}
          onClose={() => {
            setUserModalOpen(false)
          }}></UserModal>
        <OrganizationModal
          open={organizationModalOpen}
          id={organizationModalParams?.id}
          parent={
            organizationModalParams && organizationModalParams.parent
              ? {
                  id: organizationModalParams.parent.key,
                  name: organizationModalParams.parent.title,
                  parentId: organizationModalParams.parent.pId,
                  hasChild: !organizationModalParams.parent.isLeaf,
                }
              : undefined
          }
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
                onSelect={onOrganizationSelect}
                onExpand={(keys: Key[]) => {
                  setOrganizationTreeExpandedKeys(keys as string[])
                }}></Tree>
              <Button
                onClick={() => {
                  setOrganizationModalParams({
                    id: '',
                    parent: undefined,
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
                  onChange={(v) => {
                    setStatus(v)
                  }}
                  options={[
                    { value: 'all', label: '全部' },
                    { value: 'enabled', label: '已启用' },
                    { value: 'disabled', label: '已暂停' },
                  ]}
                />
                <Search
                  onChange={(e) => {
                    setKeyword(e.target.value)
                  }}
                  placeholder="请输入用户名、手机号"
                  allowClear
                  style={{ width: 200 }}
                  onSearch={() => {
                    if (organizationTreeSelectedKeys && organizationTreeSelectedKeys[0]) {
                      loadUsers(organizationTreeSelectedKeys[0], keyword, status, pagination.pageSize, pagination.current)
                    }
                  }}
                />
                <Flex justify="flex-end" align="flex-end">
                  <Button
                    type="primary"
                    onClick={() => {
                      // 若有选中机构
                      if (organizationTreeSelectedKeys && organizationTreeSelectedKeys.length > 0) {
                        const key = organizationTreeSelectedKeys[0]
                        const organization = organizationTreeDict[key]
                        setUserProps({
                          id: undefined,
                          organization: {
                            id: organization.key,
                            parentId: organization.pId,
                            name: organization.title,
                            hasChild: organization.isLeaf,
                          },
                        })
                      }
                      // 未选中机构， 理论上不应该出现
                      else {
                        setUserProps({
                          id: undefined,
                          organization: undefined,
                        })
                      }
                      setUserModalOpen(true)
                    }}>
                    添加
                  </Button>
                  <Button
                    disabled={userSelected ? false : true}
                    type="primary"
                    onClick={() => {
                      if (userSelected) {
                        setUserProps({
                          id: userSelected.id,
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
                    <Button disabled={userSelected ? false : true} type="primary">
                      删除
                    </Button>
                  </Popconfirm>
                  <Button
                    type="primary"
                    disabled={userSelected ? false : true}
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
                  onChange: (selectedRowKeys: React.Key[], selectedRows: UserDto[]) => {
                    setUserSelectedKeys(selectedRowKeys as string[])
                    if (selectedRows && selectedRows.length === 1) {
                      setUserSelected(selectedRows[0])
                    } else {
                      setUserSelected(undefined)
                    }
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
