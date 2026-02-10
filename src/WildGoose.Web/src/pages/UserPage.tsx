import { PageContainer } from "@ant-design/pro-layout"
import {
  Breadcrumb,
  Button,
  Card,
  Dropdown,
  Flex,
  Input,
  Menu,
  MenuProps,
  Modal,
  Popconfirm,
  Select,
  Space,
  Spin,
  Switch,
  Table,
  Tag,
  Tooltip,
  Tree,
  message,
  Typography,
} from "antd"
import {
  getSubOrganizationList,
  deleteOrganization,
  getUsers,
  deleteUser,
  enableUser,
  disableUser,
  addOrganizationAdministrator,
  deleteOrganizationAdministrator,
  searchOrganization,
} from "../services/wildgoose/api"
import { Key, useCallback, useEffect, useState } from "react"
import OrganizationModal from "../components/OrganizationModal"
import {
  AppstoreAddOutlined,
  CaretDownOutlined,
  DeleteOutlined,
  FormOutlined,
  MoreOutlined,
} from "@ant-design/icons"
import UserModal from "../components/UserModal"
import ChangePasswordModal from "../components/ChangePasswordModal"
import { ObjectId } from "bson"
import { EventDataNode } from "antd/es/tree"
import { ColumnType } from "antd/es/table"
import IconFont from "../iconfont/IconFont"
import { getUser } from "../lib/auth"
import { debounce } from "lodash-es"
import { ApartmentOutlined } from "@ant-design/icons"

const { Search } = Input
const { Text } = Typography

type MenuItem = Required<MenuProps>["items"][number]

const UserPage = (props?: { breadcrumb?: boolean }) => {
  const [keyword, setKeyword] = useState<string>("")
  const [searchKeyword, setSearchKeyword] = useState<string>("")
  const [searchResults, setSearchResults] = useState<MenuItem[]>([])
  const [loading, setLoading] = useState(false)

  const [status, setStatus] = useState("all")
  const [isRecursive, setIsRecursive] = useState("true")
  const [isAdmin, setIsAdmin] = useState<boolean>(false)
  const [dataSource, setDataSource] = useState<UserDto[]>([])
  const [pagination, setPagination] = useState({
    current: 1,
    pageSize: window.wildgoose.pageSize,
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
      title: "姓名",
      dataIndex: "name",
      key: "name",
    },
    {
      title: "帐号",
      dataIndex: "userName",
      key: "userName",
    },
    {
      title: "手机号",
      dataIndex: "phoneNumber",
      key: "phoneNumber",
    },
    {
      title: "管理员",
      dataIndex: "isAdministrator",
      key: "isAdministrator",
      render: (_: unknown, record) => {
        if (
          !organizationTreeSelectedKeys ||
          organizationTreeSelectedKeys.length === 0 ||
          organizationTreeSelectedKeys[0] === ""
        ) {
          return <></>
        }
        return (
          <>
            <Switch
              checked={record.isAdministrator}
              onChange={async () => {
                if (organizationTreeSelectedKeys && organizationTreeSelectedKeys.length > 0) {
                  const key = organizationTreeSelectedKeys[0]
                  if (!key) {
                    return
                  }
                  if (record.isAdministrator) {
                    Modal.confirm({
                      title: "警告",
                      content: "您确定要移除此管理员吗?",
                      onOk: async () => {
                        await deleteOrganizationAdministrator(key, record.id)
                        record.isAdministrator = !record.isAdministrator
                        record.roles = record.roles.filter((item) => item !== "organization-admin")
                        setDataSource([...dataSource])
                      },
                    })
                  } else {
                    await addOrganizationAdministrator(key, record.id)
                    record.isAdministrator = !record.isAdministrator
                    record.roles.push("organization-admin")
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
      title: "启用",
      dataIndex: "enabled",
      key: "enabled",
      render: (_: unknown, record) => {
        return (
          <>
            <Switch
              checked={record.enabled}
              onChange={async () => {
                if (record.enabled) {
                  Modal.confirm({
                    title: "警告",
                    content: "您确定要禁用此用户吗?",
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
      title: "角色",
      dataIndex: "roles",
      key: "roles",
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
      title: "所在机构",
      dataIndex: "organizations",
      key: "organizations",
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
      title: "创建时间",
      dataIndex: "creationTime",
      key: "creationTime",
    },
  ]

  const clean = () => {
    setKeyword("")
    setDataSource([])
    setPagination({
      current: 1,
      pageSize: window.wildgoose.pageSize,
      total: 0,
    })
  }

  /**
   * 设置用户是否 admin 状态
   * todo: 未来可以考虑用全局 UserContext
   */
  useEffect(() => {
    const parseIsAdmin = async () => {
      const user = await getUser()
      if (user && user.profile && user.profile.role) {
        setIsAdmin(user.profile.role.includes("admin"))
      }
    }
    parseIsAdmin()
  }, [])

  useEffect(() => {
    clean()
    const init = async () => {
      const dict: Dictionary<SimpleDataNode> = {}
      const res = await getSubOrganizationList()
      const organizations = (res?.data ?? []) as OrganizationDto[]
      const defaultNode: SimpleDataNode = {
        key: "",
        title: "默认机构",
        pId: "",
        isLeaf: true,
        children: [],
      }

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

        // 拥有 admin 角色才显示默认机构
        if (isAdmin) {
          data.push(defaultNode)
        }
        setOrganizationTreeData(data)
        setOrganizationTreeSelectedKeys([organizations[0].id])
        loadUsers(organizations[0].id, "", "all", window.wildgoose.pageSize, 1)
      } else {
        setOrganizationTreeData(isAdmin ? [defaultNode] : [])
        setOrganizationTreeSelectedKeys([""])
      }
      setOrganizationTreeDict(dict)
    }
    init()
  }, [isAdmin])

  const debouncedSearch = useCallback(
    (keyword: string) => {
      const searchFn = debounce(async (kw) => {
        if (!kw.trim()) {
          setSearchResults([])
          return
        }

        setLoading(true)
        try {
          const res = await searchOrganization(keyword)
          const results = (res.data as OrganizationSearchResultDto[]) ?? []
          setSearchResults(
            results.map((t) => ({
              key: t.id,
              label: t.name,
              title: t.fullName,
              icon: <ApartmentOutlined />,
            })),
          )
        } catch (error) {
          console.error("搜索出错:", error)
        } finally {
          setLoading(false)
        }
      }, 500)

      searchFn(keyword)
      return searchFn.cancel
    },
    [setSearchResults, setLoading],
  )

  const handleInputChange = (e: any) => {
    setSearchKeyword(e.target.value)
    debouncedSearch(e.target.value)
  }

  const handleSearch = (value: string, _: any, info?: { source?: "clear" | "input" }) => {
    if (info?.source === "clear") {
      setSearchKeyword("")
      setSearchResults([])

      // 重置原来的选中机构，以及选中的用户等
      clearUserDataSource()
    } else {
      debouncedSearch(value)
    }
  }

  const clearUserDataSource = () => {
    setDataSource([])
    setUserSelectedKeys([])
    setUserSelected(undefined)
    setOrganizationTreeSelectedKeys([])
  }

  const loadUsers = async (
    orgId: string,
    q: string,
    status: string,
    limit: number,
    page: number,
  ) => {
    const res = await getUsers({
      organizationId: orgId,
      q: q,
      limit: limit,
      isRecursive: isRecursive === "true",
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
      message.error("数据格式异常")
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

    setKeyword("")
    setStatus("all")
    if (keys.length > 0) {
      const key = keys[0] as string
      setOrganizationTreeSelectedKeys(keys as string[])
      loadUsers(key, "", "all", window.wildgoose.pageSize, 1)
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

    organizationTreeDict[values.id] = organization
    setOrganizationTreeDict({ ...organizationTreeDict })
    // 新添加了根机构
    if (!organization.pId) {
      setOrganizationTreeData(organizationTreeData.concat(organization))
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
    const res = await deleteOrganization(item.key)
    if (res) {
      if (item.pId) {
        const parent = organizationTreeDict[item.pId]
        parent.children = parent.children.filter((y) => y.key !== item.key)
        parent.isLeaf = parent.children.length === 0
        setOrganizationTreeData([...organizationTreeData])
      } else {
        setOrganizationTreeData((origin) => origin.filter((y) => y.key !== item.key))
      }
    }
  }

  const onOrganizationModalClose = () => {
    setOrganizationModalParams(undefined)
    setOrganizationModalOpen(false)
  }

  const organizationTreeTitleRender = (node: SimpleDataNode) => {
    const disabled = node.key === "" || !node.key
    const items: MenuItem[] = [
      {
        label: "编辑",
        key: "a",
        icon: <FormOutlined />,
        disabled: disabled,
        onClick: () => {
          // if (organizationTreeSelectedKeys.length === 0) {
          //   message.error("未选中机构")
          //   return
          // }
          setOrganizationModalParams({
            id: node.key,
            parent: undefined,
          })
          setOrganizationModalOpen(true)
        },
      },
      {
        label: "添加",
        key: "b",
        icon: <AppstoreAddOutlined />,
        disabled: disabled,
        onClick: () => {
          if (organizationTreeSelectedKeys.length === 0) {
            message.error("未选中机构")
            return
          }

          const org = organizationTreeDict[organizationTreeSelectedKeys[0]]
          setOrganizationModalParams({
            id: "",
            parent: org,
          })
          setOrganizationModalOpen(true)
        },
      },
      {
        label: "删除",
        key: "c",
        icon: <DeleteOutlined />,
        disabled: disabled,
        onClick: () => {
          Modal.confirm({
            title: "警告",
            content: "确认要删除这个机构吗？",
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
        {isAdmin && (
          <Dropdown
            trigger={["click"]}
            key={node.key + "_dropdown"}
            menu={{
              items,
              onClick: (ev: any) => {
                ev && ev.domEvent && ev.domEvent.stopPropagation()
              },
            }}
            placement="bottomLeft"
            arrow={false}
          >
            <MoreOutlined
              style={{ fontSize: 20 }}
              onClick={(ev: any) => {
                ev.stopPropagation()
              }}
            />
          </Dropdown>
        )}
      </>
    )
  }

  const onUserDelete = async () => {
    if (userSelectedKeys && userSelectedKeys.length > 0) {
      const key = userSelectedKeys[0]
      try {
        await deleteUser(key)
        message.success("操作成功")
      } catch (err) {
        if (typeof err === "string") {
          // axios request 已提示错误
          console.error(err)
        } else if (err instanceof Error) {
          message.error(err.message ?? "未知错误")
        }
      } finally {
        loadUsers(
          organizationTreeSelectedKeys[0],
          keyword,
          status,
          pagination.pageSize,
          pagination.current,
        )
      }
    }
  }

  const onOrganizationClick: MenuProps["onClick"] = (e) => {
    setKeyword("")
    setStatus("all")
    loadUsers(e.key, "", "all", window.wildgoose.pageSize, 1)
  }

  const renderTree = () => (
    <Tree
      className="organizationTree"
      showLine
      icon={<IconFont type="icon-zuzhijigou" />}
      showIcon={true}
      switcherIcon={<CaretDownOutlined />}
      treeData={organizationTreeData}
      loadData={onOrganizationTreeLoadData}
      expandedKeys={organizationTreeExpandedKeys}
      titleRender={organizationTreeTitleRender}
      selectedKeys={organizationTreeSelectedKeys}
      onSelect={onOrganizationSelect}
      onExpand={(keys: Key[]) => {
        setOrganizationTreeExpandedKeys(keys as string[])
      }}
    ></Tree>
  )

  const renderSearchResult = () =>
    loading ? (
      <div style={{ textAlign: "center", padding: "20px 0" }}>
        <Spin size="large" />
        <div style={{ marginTop: 16 }}>搜索中...</div>
      </div>
    ) : searchResults.length > 0 ? (
      <Menu
        inlineIndent={4}
        onClick={onOrganizationClick}
        style={{ width: 210 }}
        mode="inline"
        items={searchResults}
      />
    ) : (
      <div style={{ textAlign: "center", padding: "20px 0" }}>
        <Text type="secondary">没有找到匹配的结果</Text>
      </div>
    )

  return (
    <>
      <PageContainer
        token={{
          paddingInlinePageContainerContent: 20,
        }}
        title={false}
        breadcrumbRender={() => {
          return (
            (!props || props.breadcrumb !== false) && (
              <Breadcrumb
                style={{
                  marginTop: 10,
                }}
                items={[
                  {
                    title: "首页",
                  },
                  {
                    title: "用户管理",
                  },
                ]}
              />
            )
          )
        }}
      >
        <ChangePasswordModal
          id={userSelectedKeys && userSelectedKeys.length === 1 ? userSelectedKeys[0] : ""}
          open={changePasswordModalOpen}
          onClose={() => {
            setChangePasswordModalOpen(false)
          }}
        ></ChangePasswordModal>
        <UserModal
          id={userProps?.id}
          organization={userProps?.organization}
          open={userModalOpen}
          onOk={async () => {
            setUserModalOpen(false)

            // const record = dataSource.find((item) => item.id === user.id)
            // if (record) {
            //   record.creationTime = user.creationTime
            //   record.enabled = user.enabled
            //   record.name = user.name
            //   record.organizations = user.organizations
            //   record.phoneNumber = user.phoneNumber
            //   record.roles = user.roles
            //   record.userName = user.userName
            //   // record.isAdministrator = user.isAdministrator 是否管理员不会在编辑页面修改
            // }
            // setDataSource([...dataSource])
            await loadUsers(
              organizationTreeSelectedKeys[0],
              keyword,
              status,
              pagination.pageSize,
              pagination.current,
            )
          }}
          onClose={() => {
            setUserModalOpen(false)
          }}
        ></UserModal>
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
          onOk={onOrganizationModalOk}
        ></OrganizationModal>
        <Flex gap="middle">
          <Card>
            <Flex vertical>
              <Flex>
                <Search
                  placeholder="输入关键词搜索..."
                  value={searchKeyword}
                  onChange={handleInputChange}
                  onSearch={handleSearch}
                  allowClear
                  style={{ width: 200, marginBottom: 20, marginRight: 10 }}
                />
                {isAdmin && (
                  <Tooltip title="添加机构">
                    <Button
                      shape="circle"
                      onClick={() => {
                        setOrganizationModalParams({
                          id: "",
                          parent: undefined,
                        })
                        setOrganizationModalOpen(true)
                      }}
                    >
                      <AppstoreAddOutlined />
                    </Button>
                  </Tooltip>
                )}
              </Flex>
              {searchKeyword.trim() ? renderSearchResult() : renderTree()}
            </Flex>
          </Card>
          <Card style={{ width: "100%", overflow: "hidden" }}>
            <Flex gap="middle" align="start" vertical>
              <Flex align="start">
                <Space wrap>
                  <Select
                    defaultValue="all"
                    style={{ width: 120 }}
                    onChange={(v) => {
                      setStatus(v)
                    }}
                    options={[
                      { value: "all", label: "全部" },
                      { value: "enabled", label: "已启用" },
                      { value: "disabled", label: "已暂停" },
                    ]}
                  />{" "}
                  <Select
                    defaultValue="true"
                    style={{ width: 165 }}
                    onChange={(v) => {
                      setIsRecursive(v)
                    }}
                    options={[
                      { value: "true", label: "展示全部成员" },
                      { value: "false", label: "仅展示部门直属成员" },
                    ]}
                  />
                  <Search
                    onChange={(e) => {
                      setKeyword(e.target.value)
                    }}
                    placeholder="查询的账号、手机号"
                    allowClear
                    style={{ width: 220 }}
                    onSearch={() => {
                      if (organizationTreeSelectedKeys) {
                        loadUsers(
                          organizationTreeSelectedKeys[0],
                          keyword,
                          status,
                          pagination.pageSize,
                          pagination.current,
                        )
                      }
                    }}
                  />
                  <Flex justify="flex-end" align="flex-end">
                    <Space wrap>
                      <Button
                        type="primary"
                        onClick={() => {
                          debugger
                          message.success("添加子机构")
                          return
                          // 若有选中机构
                          if (
                            organizationTreeSelectedKeys &&
                            organizationTreeSelectedKeys.length > 0
                          ) {
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
                        }}
                      >
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
                        }}
                      >
                        编辑
                      </Button>
                      <Popconfirm
                        title="警告"
                        description="您确定要删除此用户吗?"
                        onConfirm={() => {
                          onUserDelete()
                        }}
                        okText="确定"
                        cancelText="取消"
                      >
                        <Button disabled={userSelected ? false : true} type="primary">
                          删除
                        </Button>
                      </Popconfirm>
                      <Button
                        type="primary"
                        disabled={userSelected ? false : true}
                        onClick={() => {
                          setChangePasswordModalOpen(true)
                        }}
                      >
                        修改密码
                      </Button>
                      {/* <Button type="primary">导出</Button>
                      <Button type="primary">导入</Button> */}
                    </Space>
                  </Flex>
                </Space>
              </Flex>
              <Table
                rowKey="id"
                columns={columns}
                dataSource={dataSource}
                pagination={{
                  ...pagination,
                  onChange: (page: number) => {
                    loadUsers(
                      organizationTreeSelectedKeys[0],
                      keyword,
                      status,
                      pagination.pageSize,
                      page,
                    )
                  },
                }}
                rowSelection={{
                  type: "checkbox",
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
                bordered
                scroll={{ x: "max-content" }}
                style={{ width: "100%" }}
              ></Table>
            </Flex>
          </Card>
        </Flex>
      </PageContainer>
    </>
  )
}

export default UserPage
