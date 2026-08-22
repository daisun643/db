import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { getRequiredPermissions } from './routeAccess'

const routes = [
  { 
    path: '/', 
    component: () => import('../views/HomeView.vue'),
    meta: { requiresAuth: true }
  },
  { 
    path: '/login', 
    component: () => import('../views/LoginView.vue'),
    meta: { guest: true }
  },
  { 
    path: '/register', 
    component: () => import('../views/RegisterView.vue'),
    meta: { guest: true }
  },
  { 
    path: '/forgot-password', 
    component: () => import('../views/ForgotPasswordView.vue'),
    meta: { guest: true }
  },
  { 
    path: '/forums', 
    component: () => import('../views/ForumsView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/forums') }
  },
  { 
    path: '/products', 
    component: () => import('../views/ProductsView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/products') }
  },
  { 
    path: '/messages', 
    component: () => import('../views/MessagesView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/messages') }
  },
  { 
    path: '/profile', 
    component: () => import('../views/ProfileView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/profile') }
  },
  { 
    path: '/user/:id', 
    component: () => import('../views/UserProfileView.vue'),
    meta: { requiresAuth: true }
  },
  { 
    path: '/system-status', 
    component: () => import('../views/SystemStatusView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/system-status') }
  },
  {
    path: '/finance',
    component: () => import('../views/FinanceView.vue'),
    meta: { requiresAuth: true}
}
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach(async (to, from, next) => {
  const authStore = useAuthStore()
  
  if (!authStore.user && !authStore.loading) {
    await authStore.fetchUser()
  }

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    next('/login')
  } else if (to.meta.guest && authStore.isAuthenticated) {
    next('/')
  } else if (to.meta.requiresBackendRouteCheck) {
    if (!authStore.hasAnyPermission(to.meta.requiredPermissions || [])) {
      next('/')
      return
    }

    const hasAccess = await authStore.checkRouteAccess(to.path, to.meta.requiredPermissions || [])
    if (hasAccess) {
      next()
    } else {
      next('/')
    }
  } else {
    next()
  }
})

export default router
