import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { getRequiredPermissions } from './routeAccess'

const routes = [
  { 
    path: '/', 
    component: () => import('../views/home/HomeView.vue'),
    meta: { requiresAuth: true }
  },
  { 
    path: '/login', 
    component: () => import('../views/auth/LoginView.vue'),
    meta: { guest: true }
  },
  { 
    path: '/register', 
    component: () => import('../views/auth/RegisterView.vue'),
    meta: { guest: true }
  },
  { 
    path: '/forgot-password', 
    component: () => import('../views/auth/ForgotPasswordView.vue'),
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
        path: 'search',
        component: () => import('../views/forum/ForumSearchView.vue'),
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
          { path: 'joined', component: () => import('../views/forum/MyJoinedForumsView.vue') },
          { path: 'favorites', component: () => import('../views/forum/FavoritesView.vue') },
        ],
      },
    ],
  },
  { 
    path: '/products', 
    component: () => import('../views/market/ProductsView.vue'),
    // 子路由共用 /products 的权限校验（后端路由表只认 /products）
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/products'), accessPath: '/products' },
    children: [
      {
        path: '',
        component: () => import('../views/market/AllProductsView.vue'),
      },
      {
        path: 'my',
        component: () => import('../views/market/MyProductsView.vue'),
      },
      {
        path: 'orders',
        component: () => import('../views/market/OrdersView.vue'),
      },
    ],
  },
  { 
    path: '/messages', 
    component: () => import('../views/social/MessagesView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/messages') }
  },
  { 
    path: '/profile', 
    component: () => import('../views/user/ProfileView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/profile') }
  },
  { 
    path: '/user/:id', 
    component: () => import('../views/user/UserProfileView.vue'),
    meta: { requiresAuth: true }
  },
  { 
    path: '/system-status', 
    component: () => import('../views/system/SystemStatusDashboardView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/system-status') }
  },
  {
    path: '/system-status/forums',
    component: () => import('../views/system/SystemForumsView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/system-status'), accessPath: '/system-status' }
  },
  {
    path: '/system-status/users',
    component: () => import('../views/system/SystemUsersView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/system-status'), accessPath: '/system-status' }
  },
  {
    path: '/system-status/audits',
    component: () => import('../views/system/SystemAuditsView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/system-status'), accessPath: '/system-status' }
  },
  {
    path: '/system-status/posts',
    component: () => import('../views/system/SystemPostsView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/system-status'), accessPath: '/system-status' }
  },
  {
    path: '/system-status/disputes',
    component: () => import('../views/system/SystemDisputesView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/system-status'), accessPath: '/system-status' }
  },
  {
    path: '/system-status/reports',
    component: () => import('../views/system/SystemReportsView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/system-status'), accessPath: '/system-status' }
  },
  {
    path: '/system-status/announcements',
    component: () => import('../views/system/SystemAnnouncementsView.vue'),
    meta: { requiresAuth: true, requiresBackendRouteCheck: true, requiredPermissions: getRequiredPermissions('/system-status'), accessPath: '/system-status' }
  },
  {
    path: '/finance',
    component: () => import('../views/market/FinanceView.vue'),
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
