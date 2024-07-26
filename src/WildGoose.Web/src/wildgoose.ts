import dayjs from "dayjs"

declare global {
  interface Window {
    wildgoose: {
      backend: string
      baseName: string
      pageSize: number
      applicationId: string
      oidc: any
    }
  }

  interface Defaults {
    pageSize: number
  }

  interface Dictionary<TValue> {
    [key: string]: TValue
  }

  interface SimpleDataNode {
    pId: string
    children: SimpleDataNode[]
    key: string
    title: string
    isLeaf: boolean
  }

  type OrganizationTreeNode = {
    id: string
    pId: string
    title: string
    value: string
    isLeaf: boolean
  }

  export interface PageData<T> {
    data: T[]
    limit: number
    page: number
    total: number
  }

  type OrganizationDto = {
    id: string
    name: string
    parentId: string
    hasChild: boolean
  }

  type Organization = {
    id: string
    name: string
    code: string
    address: string
    description: string
    parentId: string
    scope: string[]
    administrator: {
      id: string
      name: string
    }[]
  }

  type UserDto = {
    creationTime: string
    enabled: boolean
    id: string
    name: string
    organizations: string[]
    phoneNumber: string
    roles: string[]
    userName: string
    isAdministrator: boolean
  }

  interface RoleBasicDto {
    id: string
    name: string
  }

  type AssignableRoleDto = {
    id: string
    name: string
  }

  type RoleDto = {
    assignableRoles: AssignableRoleDto[]
    description: string
    id: string
    lastModificationTime: string
    name: string
    statement: string
    version: number
  }

  type UserDetailDto = {
    code?: string
    departureTime?: number
    email?: string
    hiddenSensitiveData: boolean
    name?: string
    organizations: OrganizationDto[]
    phoneNumber?: string
    roles: RoleBasicDto[]
    title?: string
    userName: string
  }

  type UpdateUserDto = {
    code?: string
    departureTime?: dayjs.Dayjs
    email?: string
    hiddenSensitiveData: boolean
    name?: string
    organizations: string[]
    phoneNumber?: string
    roles: string[]
    title?: string
    userName: string
  }

  type AddUserDto = {
    password: string
    organizations: string[]
    phoneNumber?: string
    name?: string
    userName: string
  }
}

export {}
