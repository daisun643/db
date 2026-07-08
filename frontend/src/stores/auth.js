import { defineStore } from 'pinia'
import { getCurrentUser, login, logout, register, checkRouteAccess } from '../api'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: null,
    isAuthenticated: false,
    loading: false,
    routeAccessCache: {},
  }),

  actions: {
    async fetchUser() {
      try {
        this.loading = true
        const response = await getCurrentUser()
        if (response.data.success) {
          this.user = response.data.user
          this.isAuthenticated = true
          this.routeAccessCache = {}
        }
      } catch (error) {
        this.user = null
        this.isAuthenticated = false
        this.routeAccessCache = {}
      } finally {
        this.loading = false
      }
    },

    async login(credentials) {
      const response = await login(credentials)
      if (response.data.success) {
        this.user = response.data.user
        this.isAuthenticated = true
        this.routeAccessCache = {}
      }
      return response.data
    },

    async register(data) {
      const response = await register(data)
      if (response.data.success) {
        this.user = response.data.user
        this.isAuthenticated = true
        this.routeAccessCache = {}
      }
      return response.data
    },

    async logout() {
      await logout()
      this.user = null
      this.isAuthenticated = false
      this.routeAccessCache = {}
    },

    hasRole(roleName) {
      return (this.user?.roles || [])
        .some(role => role.toLowerCase() === roleName.toLowerCase())
    },

    hasAnyPermission(requiredPermissions = []) {
      if (!requiredPermissions || requiredPermissions.length === 0) {
        return this.isAuthenticated
      }

      if (this.hasRole('Admin')) {
        return true
      }

      const permissions = new Set(
        (this.user?.permissions || []).map(permission => permission.toLowerCase())
      )
      return requiredPermissions.some(permission => permissions.has(permission.toLowerCase()))
    },

    async checkRouteAccess(path, requiredPermissions = null) {
      if (this.routeAccessCache[path] !== undefined) {
        return this.routeAccessCache[path]
      }

      try {
        const response = await checkRouteAccess(path)
        const hasAccess = response.data.hasAccess
        this.routeAccessCache[path] = hasAccess
        if (requiredPermissions && hasAccess !== this.hasAnyPermission(requiredPermissions)) {
          console.warn('前后端路由权限判断不一致', {
            path,
            requiredPermissions,
            backendAccess: hasAccess,
          })
        }
        return hasAccess
      } catch (error) {
        console.error('检查路由权限失败:', error)
        return false
      }
    },
  },
})
