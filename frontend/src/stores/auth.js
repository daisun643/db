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
        }
      } catch (error) {
        this.user = null
        this.isAuthenticated = false
      } finally {
        this.loading = false
      }
    },

    async login(credentials) {
      const response = await login(credentials)
      if (response.data.success) {
        this.user = response.data.user
        this.isAuthenticated = true
      }
      return response.data
    },

    async register(data) {
      const response = await register(data)
      if (response.data.success) {
        this.user = response.data.user
        this.isAuthenticated = true
      }
      return response.data
    },

    async logout() {
      await logout()
      this.user = null
      this.isAuthenticated = false
      this.routeAccessCache = {}
    },

    async checkRouteAccess(path) {
      if (this.routeAccessCache[path] !== undefined) {
        return this.routeAccessCache[path]
      }

      try {
        const response = await checkRouteAccess(path)
        const hasAccess = response.data.hasAccess
        this.routeAccessCache[path] = hasAccess
        return hasAccess
      } catch (error) {
        console.error('检查路由权限失败:', error)
        return false
      }
    },
  },
})
