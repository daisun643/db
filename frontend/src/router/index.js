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
    component: () => import('../views/forum/ForumLayoutView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/forums'), accessPath: '/forums' },
    children: [
      {
        path: '',
        component: () => import('../views/forum/ForumHomeView.vue'),
      },
      {
        path: 'board/:forumId(\\d+)',
        component: () => import('../views/forum/ForumBoardView.vue'),
      },
      {
        path: 'post/:postId(\\d+)',
        component: () => import('../views/forum/PostDetailView.vue'),
      },
      {
        path: 'my',
        component: () => import('../views/forum/MyView.vue'),
        children: [
          { path: '', redirect: '/forums/my/posts' },
          { path: 'posts', component: () => import('../views/forum/MyPostsView.vue') },
          { path: 'comments', component: () => import('../views/forum/MyCommentsView.vue') },
          { path: 'forums', component: () => import('../views/forum/MyForumsView.vue') },
          { path: 'favorites', component: () => import('../views/forum/FavoritesView.vue') },
        ],
      },
    ],
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
  // 前进/后退时恢复滚动位置，新导航回到顶部（配合论坛列表页 keep-alive）
  scrollBehavior(to, from, savedPosition) {
    if (savedPosition) return savedPosition
    return { top: 0 }
  },
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

    const hasAccess = await authStore.checkRouteAccess(to.meta.accessPath || to.path, to.meta.requiredPermissions || [])
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
