import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

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
    meta: { requiresAuth: true, requiresBackendRouteCheck: true }
  },
  { 
    path: '/products', 
    component: () => import('../views/ProductsView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true }
  },
  { 
    path: '/messages', 
    component: () => import('../views/MessagesView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true }
  },
  { 
    path: '/profile', 
    component: () => import('../views/ProfileView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true }
  },
  { 
    path: '/system-status', 
    component: () => import('../views/SystemStatusView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true }
  },
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
    const hasAccess = await authStore.checkRouteAccess(to.path)
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
