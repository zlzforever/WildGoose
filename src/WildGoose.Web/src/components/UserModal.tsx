import {
  Form,
  Row,
  Col,
  Input,
  Modal,
  TreeSelect,
  TreeSelectProps,
  Checkbox,
  DatePicker,
  Select,
  SelectProps,
  message,
} from "antd"
import { useEffect, useRef, useState } from "react"
import {
  getSubOrganizationList,
  getUser,
  updateUser,
  addUser,
  getAssignableRoles,
} from "../services/wildgoose/api"
import * as dayjs from "dayjs"

const phoneValidator = (_: any, value: any, callback: any) => {
  if (value) {
    // 跳过掩码值（包含 * 说明是已通过验证的手机号）
    if (typeof value === "string" && value.includes("*")) {
      return callback()
    }
    const reg: any = /^(13[0-9]|14[01456879]|15[0-35-9]|16[2567]|17[0-8]|18[0-9]|19[0-35-9])\d{8}$/
    if (reg.test(value)) {
      return callback()
    }
    return Promise.reject(new Error("手机号无效"))
  }
  return callback()
}

/** 如果值是手机号格式，则用 * 隐藏中间部分，只保留前 2 和后 2 位 */
const maskPhoneNumber = (value?: string): string => {
  if (value && /^1\d{10}$/.test(value)) {
    return value.slice(0, 2) + "******" + value.slice(-2)
  }
  return value ?? ""
}

export interface UserProps {
  id?: string
  organization?: OrganizationDto
}

export interface UserModalProps extends UserProps {
  open?: boolean
  onClose?: () => void
  onOk?: (user: UserDto) => void
}

const UserModal: React.FC<UserModalProps> = (props) => {
  const [form] = Form.useForm<UpdateUserDto>()
  const rawValuesRef = useRef<Record<string, string>>({})
  const [organizationTreeData, setOrganizationTreeData] = useState<OrganizationTreeNode[]>([])
  // const [organizationTreeSelectedKeys, setOrganizationTreeSelectedKeys] = useState<string[]>()
  const [organizationTreeDict, setOrganizationTreeDict] = useState<
    Dictionary<OrganizationTreeNode>
  >({})
  const [roleOptions, setRoleOptions] = useState<SelectProps["options"]>()
  const [propertyDefs, setPropertyDefs] = useState<{ name: string; displayName: string }[]>([])

  const title = props.id ? "编辑用户" : "添加用户"

  // 初始化机构选择器
  useEffect(() => {
    const init = async () => {
      if (!props.open) {
        return
      }
      const roleReps = await getAssignableRoles()
      const roles = (roleReps.data as RoleBasicDto[]).map((x) => {
        return {
          value: x.name,
          label: x.name,
        }
      })
      const cache: Dictionary<OrganizationTreeNode> = {}
      const res = await getSubOrganizationList("")
      const subOrganizations = (res.data as OrganizationDto[]) ?? []
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
      // 若初始查询出的机构不含有传入的机构， 则把传入的机构并入数组
      if (
        props.organization &&
        organizations.findIndex((item) => item.id === props.organization?.id) === -1
      ) {
        const node = {
          id: props.organization.id,
          pId: props.organization.parentId,
          value: props.organization.id,
          title: props.organization.name,
          isLeaf: !props.organization.hasChild,
        }
        cache[props.organization.id] = node
        organizations.push(node)
      }

      form.resetFields()

      // 创建
      if (!props.id) {
        const user: AddUserDto = {
          organizations: [],
          password: "",
          userName: "",
        }
        // 上级机构初始化
        if (props.organization?.id) {
          user.organizations = [props.organization.id]
        } else {
          //
        }

        form.setFieldsValue(user)
      }
      // 编辑
      else {
        const values: UpdateUserDto = {
          organizations: [],
          roles: [],
          userName: "",
          hiddenSensitiveData: false,
        }
        const res = await getUser(props.id)
        if (!res.data) {
          message.error("用户信息为空")
          return
        }
        const userDetail = res.data as UserDetailDto
        const rawName = userDetail.name ?? ""
        const rawUserName = userDetail.userName ?? ""
        const rawPhone = userDetail.phoneNumber ?? ""
        rawValuesRef.current = { name: rawName, userName: rawUserName, phoneNumber: rawPhone }
        values.code = userDetail.code
        values.email = userDetail.email
        values.hiddenSensitiveData = userDetail.hiddenSensitiveData
        values.name = maskPhoneNumber(rawName)
        values.phoneNumber = maskPhoneNumber(rawPhone)
        values.title = userDetail.title
        values.userName = maskPhoneNumber(rawUserName)

        if (userDetail.departureTime) {
          values.departureTime = dayjs.unix(userDetail.departureTime)
        }

        // 加载扩展属性到 form，同时缓存定义用于显示标签
        if (userDetail.properties && userDetail.properties.length > 0) {
          values.properties = {}
          const defs: { name: string; displayName: string }[] = []
          userDetail.properties.forEach((p) => {
            if (values.properties) {
              values.properties[p.name] = p.value ?? ""
            }
            defs.push({ name: p.name, displayName: p.displayName })
          })
          setPropertyDefs(defs)
        }

        concatOrganizations(organizations, userDetail.organizations, cache)

        // 加载用户父节点下的所有子节点（平级机构），
        // 避免 TreeSelect 因已存在子节点而跳过 loadData
        const loadedParentIds = new Set<string>()
        for (const org of userDetail.organizations) {
          if (org.parentId  && !loadedParentIds.has(org.parentId)) {
            loadedParentIds.add(org.parentId)
            const siblingRes = await getSubOrganizationList(org.parentId)
            const siblings = (siblingRes.data as OrganizationDto[]) ?? []
            concatOrganizations(organizations, siblings, cache)
          }
        }

        values.organizations = userDetail.organizations.map((x) => x.id)

        // 若有角色不是当前用户可授于角色（是别人授于的）也要能显示
        userDetail.roles.map((x) => {
          if (roles.findIndex((y) => y.value === x.name) === -1) {
            roles.push({
              value: x.name,
              label: x.name,
            })
          }
        })
        values.roles = userDetail.roles.map((x) => x.name)

        form.setFieldsValue(values)
      }

      setRoleOptions(roles)
      setOrganizationTreeData(organizations)
      setOrganizationTreeDict(cache)
    }
    init()
  }, [props.organization, form, props.id, props.open])

  const concatOrganizations = (
    treeData: OrganizationTreeNode[],
    subOrganizations: OrganizationDto[],
    cache: Dictionary<OrganizationTreeNode>,
  ) => {
    subOrganizations.map((x) => {
      const node = {
        id: x.id,
        pId: x.parentId,
        value: x.id,
        title: x.name,
        isLeaf: !x.hasChild,
      }
      const origin = cache[x.id]
      if (!origin) {
        cache[x.id] = node
        treeData.push(node)
      } else {
        origin.pId = node.pId
        origin.value = node.value
        origin.title = node.title
        origin.isLeaf = node.isLeaf
      }
    })
  }

  const onOrganizationLoadData: TreeSelectProps["loadData"] = async ({ id }) => {
    const res = await getSubOrganizationList(id)
    const subOrganizations = res.data as OrganizationDto[]
    if (subOrganizations && subOrganizations.length > 0) {
      concatOrganizations(organizationTreeData, subOrganizations, organizationTreeDict)
      setOrganizationTreeData([...organizationTreeData])
      setOrganizationTreeDict(organizationTreeDict)
    }
  }

  /** 聚焦时显示完整值 */
  const handleFocus = (field: "name" | "userName" | "phoneNumber") => {
    const raw = rawValuesRef.current[field]
    if (raw) {
      form.setFieldValue(field, raw)
    }
  }

  /** 失焦时若为手机号格式则掩码，并保存当前完整值 */
  const handleBlur = (field: "name" | "userName" | "phoneNumber") => {
    const value = form.getFieldValue(field)
    if (typeof value === "string") {
      rawValuesRef.current[field] = value
      if (/^1\d{10}$/.test(value)) {
        form.setFieldValue(field, maskPhoneNumber(value))
      }
    }
  }

  const onOk = async () => {
    const result = await form.validateFields()
    if (result) {
      const values = form.getFieldsValue()
      // 如果字段仍为掩码后的值，恢复为完整的原始值提交给接口
      for (const field of ["name", "userName", "phoneNumber"] as const) {
        const raw = rawValuesRef.current[field]
        if (raw && values[field] === maskPhoneNumber(raw)) {
          values[field] = raw
        }
      }
      let user
      // 编辑
      if (props.id) {
        user = (await updateUser(props.id, values)).data
      }
      // 新增
      else {
        user = (await addUser(values)).data
      }
      if (props.onOk) {
        props.onOk(user)
      }
    }
    // form.validateFields().then(async () => {})
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
          if (props.onClose) {
            props.onClose()
          }
        }}
      >
        <Form layout="vertical" form={form}>
          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="userName"
                label="帐号"
                rules={[{ required: true, message: "请输入帐号" }]}
              >
                <Input
                  placeholder="请输入帐号"
                  maxLength={36}
                  onFocus={() => handleFocus("userName")}
                  onBlur={() => handleBlur("userName")}
                />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="name"
                label="姓名"
                rules={[{ required: true, message: "请输入姓名" }]}
              >
                <Input
                  placeholder="请输入姓名"
                  maxLength={256}
                  onFocus={() => handleFocus("name")}
                  onBlur={() => handleBlur("name")}
                />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="phoneNumber"
                label="手机号"
                rules={[{ required: true, message: "请输入手机号" }, { validator: phoneValidator }]}
              >
                <Input
                  placeholder="请输入手机号"
                  maxLength={11}
                  onFocus={() => handleFocus("phoneNumber")}
                  onBlur={() => handleBlur("phoneNumber")}
                />
              </Form.Item>
            </Col>
            {props.id ? (
              <>
                <Col span={12}>
                  <Form.Item name="code" label="编号">
                    <Input placeholder="请输入编号" maxLength={36} />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item
                    name="email"
                    label="邮箱"
                    rules={[{ type: "email", message: "邮箱无效" }]}
                  >
                    <Input placeholder="请输入邮箱" maxLength={256} />
                  </Form.Item>
                </Col>
              </>
            ) : (
              <>
                {!window.wildgoose.disablePasswordLogin && (
                  <Col span={12}>
                    <Form.Item
                      name="password"
                      label="密码"
                      rules={[{ required: true, message: "请输入密码" }]}
                    >
                      <Input placeholder="请输入密码" maxLength={32} />
                    </Form.Item>
                  </Col>
                )}
              </>
            )}
            <Col span={12}>
              <Form.Item name="organizations" label="部门">
                <TreeSelect
                  allowClear={true}
                  treeLine
                  multiple={true}
                  treeData={organizationTreeData}
                  // onChange={(v) => {
                  //   setOrganizationTreeSelectedKeys(v)
                  // }}
                  treeDataSimpleMode
                  dropdownStyle={{ maxHeight: 400, overflow: "auto" }}
                  loadData={onOrganizationLoadData}
                  placeholder="部门"
                />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item name="roles" label="角色">
                <Select mode="multiple" options={roleOptions} virtual={false} />
              </Form.Item>
            </Col>
            {propertyDefs.map((def) => (
              <Col span={12} key={def.name}>
                <Form.Item name={["properties", def.name]} label={def.displayName}>
                  <Input placeholder={"请输入" + def.displayName} maxLength={1024} />
                </Form.Item>
              </Col>
            ))}
            {props.id ? (
              <>
                <Col span={12}>
                  <Form.Item name="departureTime" label="离职时间">
                    <DatePicker />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item
                    name="hiddenSensitiveData"
                    label="隐藏敏感信息"
                    valuePropName="checked"
                  >
                    <Checkbox />
                  </Form.Item>
                </Col>
              </>
            ) : (
              <></>
            )}
          </Row>
        </Form>
      </Modal>
    </>
  )
}

export default UserModal
