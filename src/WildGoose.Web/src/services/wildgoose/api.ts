import { ApiResult, request } from "../../lib/request"
import { createRequest } from "../../lib/userRequest"

export interface GetRolesQuery {
  q: string | undefined
  page: number | undefined
  limit: number | undefined
}

export async function getRoles(query?: GetRolesQuery) {
  const result = await request.request<ApiResult>({
    url: `${window.wildgoose.backend}/admin/v1.0/roles`,
    method: "GET",
    params: query,
  })
  return result.data
}

export interface CreateRoleCommand {
  name: string
  description: string
}

export async function addRole(command: CreateRoleCommand) {
  return (
    await request.request<ApiResult>({
      url: `${window.wildgoose.backend}/admin/v1.0/roles`,
      method: "POST",
      data: command,
    })
  ).data
}

export interface AddAssignableRoleCommand {
  id: string
  assignableRoleId: string
}

export async function addAssignableRole(command: AddAssignableRoleCommand[]) {
  return (
    await request.request<ApiResult>({
      url: `${window.wildgoose.backend}/admin/v1.0/roles/assignableRoles`,
      method: "POST",
      data: command,
    })
  ).data
}

export async function getAssignableRoles() {
  return (
    await request.request<ApiResult>({
      url: `${window.wildgoose.backend}/admin/v1.0/roles/assignableRoles`,
      method: "GET",
    })
  ).data
}

export async function deleteRole(id: string) {
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/roles/${id}`,
      method: "DELETE",
    })
  ).data
}

export async function deleteAssignableRole(id: string, assignableRoleId: string) {
  return (
    await request.request<ApiResult>({
      url: `${window.wildgoose.backend}/admin/v1.0/roles/${id}/assignableRoles/${assignableRoleId}`,
      method: "DELETE",
    })
  ).data
}

export async function getRole(id: string) {
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/roles/${id}`,
      method: "GET",
    })
  ).data
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export async function updateRole(id: string, values: any) {
  if (!id) {
    return
  }
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/roles/${id}`,
      method: "POST",
      data: values,
    })
  ).data
}

export async function updateRoleStatement(id: string, values: { statement: string }) {
  if (!id) {
    return
  }
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/roles/${id}/statement`,
      method: "POST",
      data: values,
    })
  ).data
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export async function getSubOrganizationList(parentId?: string) {
  const result = await request.request<ApiResult>({
    url: `${window.wildgoose.backend}/admin/v1.0/organizations/subList`,
    method: "GET",
    params: {
      parentId: parentId ? parentId : "",
    },
  })
  return result.data
}

export async function searchOrganization(keyword: string) {
  const result = await request.request<ApiResult>({
    url: `${window.wildgoose.backend}/admin/v1.0/organizations/search`,
    method: "GET",
    params: {
      keyword,
    },
  })
  return result.data
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export async function addOrganization(values: any) {
  return (
    await request.request<ApiResult>({
      url: `${window.wildgoose.backend}/admin/v1.0/organizations`,
      method: "POST",
      data: values,
    })
  ).data
}

export async function getOrganization(id: string) {
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/organizations/${id}`,
      method: "GET",
    })
  ).data
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export async function updateOrganization(id: string, values: any) {
  if (!id) {
    return
  }
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/organizations/${id}`,
      method: "POST",
      data: values,
    })
  ).data
}

export async function deleteOrganization(id: string) {
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/organizations/${id}`,
      method: "DELETE",
    })
  ).data
}

export interface GetUsersQuery {
  q: string | undefined
  organizationId: string | undefined
  status: string | undefined
  page: number | undefined
  limit: number | undefined
}

export async function getUsers(query?: GetUsersQuery) {
  const result = await request.request<ApiResult>({
    url: `${window.wildgoose.backend}/admin/v1.0/users`,
    method: "GET",
    params: query,
  })
  return result.data
}

export async function getUser(id: string) {
  const result = await request.request<ApiResult>({
    url: `${window.wildgoose.backend}/admin/v1.0/users/${id}`,
    method: "GET",
  })
  return result.data
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export async function updateUser(id: string, values: any) {
  if (!id) {
    return
  }
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/users/${id}`,
      method: "POST",
      data: values,
    })
  ).data
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export async function addUser(values: any) {
  const { post } = createRequest({ baseURL: window.wildgoose.backend })
  return (await post("/admin/v1.0/users", values)).data
}

export async function deleteUser(id: string) {
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/users/${id}`,
      method: "DELETE",
    })
  ).data
}

export async function enableUser(id: string) {
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/users/${id}/enable`,
      method: "POST",
    })
  ).data
}

export async function disableUser(id: string) {
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/users/${id}/disable`,
      method: "POST",
    })
  ).data
}

export interface ChangePasswordCommand {
  newPassword: string
  confirmPassword: string
}

export async function changePassword(id: string, command: ChangePasswordCommand) {
  const { post } = createRequest({ baseURL: window.wildgoose.backend })
  const res = await post(`/admin/v1.0/users/${id}/password`, command)
  return res.data
}

export async function deleteOrganizationAdministrator(id: string, userId: string) {
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/organizations/${id}/administrators/${userId}`,
      method: "DELETE",
    })
  ).data
}

export async function addOrganizationAdministrator(id: string, userId: string) {
  return (
    await request.request({
      url: `${window.wildgoose.backend}/admin/v1.0/organizations/${id}/administrators/${userId}`,
      method: "POST",
    })
  ).data
}
