<template>
  <aside :class="['sidebar', { collapsed: isCollapsed }]">
    <router-link to="/" class="sidebar-brand" aria-label="返回首页">
      <span class="brand-mark">济</span>
      <span v-if="!isCollapsed" class="brand-copy"><strong>同济校园</strong><small>Campus Hub</small></span>
    </router-link>
    <button
      class="toggle-btn"
      @click="toggleSidebar"
      :aria-label="isCollapsed ? '展开侧边栏' : '收起侧边栏'"
      :title="isCollapsed ? '展开侧边栏' : '收起侧边栏'"
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M15 18l-6-6 6-6" v-if="!isCollapsed" />
        <path d="M9 18l6-6-6-6" v-else />
      </svg>
    </button>

    <nav class="sidebar-nav">
      <div class="nav-section">
        <router-link to="/" class="nav-item" title="首页">
          <svg class="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
            <polyline points="9 22 9 12 15 12 15 22" />
          </svg>
          <span class="nav-label" v-if="!isCollapsed">首页</span>
        </router-link>

        <router-link v-if="canAccess('/forums')" to="/forums" class="nav-item" title="论坛">
          <svg class="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
          </svg>
          <span class="nav-label" v-if="!isCollapsed">论坛</span>
        </router-link>

        <router-link v-if="canAccess('/products')" to="/products" class="nav-item" title="交易">
          <svg class="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="9" cy="21" r="1" />
            <circle cx="20" cy="21" r="1" />
            <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6" />
          </svg>
          <span class="nav-label" v-if="!isCollapsed">交易</span>
        </router-link>

        <router-link to="/finance" class="nav-item" title="资金流水">
          <svg class="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="2" y="7" width="20" height="14" rx="2" ry="2" />
            <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" />
          </svg>
          <span class="nav-label" v-if="!isCollapsed">资金流水</span>
        </router-link>

        <router-link v-if="canAccess('/messages')" to="/messages" class="nav-item" title="消息">
          <span class="nav-icon-wrap">
            <svg class="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" />
              <polyline points="22,6 12,13 2,6" />
            </svg>
            <span v-if="unreadTotal > 0 && route.path !== '/messages'" class="nav-badge">{{ badgeText }}</span>
          </span>
          <span class="nav-label" v-if="!isCollapsed">消息</span>
        </router-link>

        <router-link v-if="canAccess('/system-status')" to="/system-status" class="nav-item" title="系统状态">
          <svg class="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M12 20h9" />
            <path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z" />
          </svg>
          <span class="nav-label" v-if="!isCollapsed">系统状态</span>
        </router-link>
      </div>

      <div class="user-section">
        <div class="user-profile" title="个人资料" @click="$router.push('/profile')">
          <div class="user-avatar">
            <img
              v-if="userAvatarUrl"
              :src="userAvatarUrl"
              :alt="authStore.user?.username || '用户'"
              @error="markAvatarFailed(authStore.user?.avatarUrl)"
            />
            <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
              <circle cx="12" cy="7" r="4" />
            </svg>
          </div>
          <div class="user-details" v-if="!isCollapsed">
            <div class="user-name">{{ authStore.user?.username || '用户' }}</div>
            <div class="user-credit">信用分: {{ authStore.user?.credit || 0 }}</div>
          </div>
        </div>
        <button class="logout-btn" @click="handleLogout" :title="isCollapsed ? '登出' : ''">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
            <polyline points="16 17 21 12 16 7" />
            <line x1="21" y1="12" x2="9" y2="12" />
          </svg>
          <span v-if="!isCollapsed">登出</span>
        </button>
      </div>
    </nav>
  </aside>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useRoute, useRouter } from 'vue-router'
import { getUnreadMessageCount, getUnreadNotificationCount } from '../api'
import { PROTECTED_MENU_PATHS, getRequiredPermissions } from '../router/routeAccess'
import { canShowAvatar, markAvatarFailed } from '../utils/avatarFallback'
import { onStreamEvent, onStreamOpen } from '../utils/notificationStream'

const authStore = useAuthStore()
const router = useRouter()
const route = useRoute()
const isCollapsed = ref(true)
const routeAccess = ref({})

// 侧边栏消息入口的未读角标：未读私信 + 未读通知（由 SSE 推送驱动刷新）
const messageUnread = ref(0)
const notificationUnread = ref(0)

const unreadTotal = computed(() => messageUnread.value + notificationUnread.value)
const badgeText = computed(() => (unreadTotal.value > 99 ? '99+' : String(unreadTotal.value)))

const loadUnreadCounts = async () => {
  if (!authStore.isAuthenticated) {
    messageUnread.value = 0
    notificationUnread.value = 0
    return
  }
  try {
    const [messageRes, notificationRes] = await Promise.all([
      getUnreadMessageCount(),
      getUnreadNotificationCount(),
    ])
    messageUnread.value = messageRes.data.count || 0
    notificationUnread.value = notificationRes.data.count || 0
  } catch {
    // 刷新失败保留上次结果，下次推送事件或路由切换会重试
  }
}

const toggleSidebar = () => {
  isCollapsed.value = !isCollapsed.value
}

const userAvatarUrl = computed(() => {
  const url = authStore.user?.avatarUrl || ''
  return canShowAvatar(url) ? url : ''
})

const canAccess = (path) => {
  const requiredPermissions = getRequiredPermissions(path)
  return authStore.hasAnyPermission(requiredPermissions || []) && routeAccess.value[path] === true
}

const updateRouteAccess = async () => {
  if (!authStore.isAuthenticated) {
    routeAccess.value = {}
    return
  }

  const entries = await Promise.all(
    PROTECTED_MENU_PATHS.map(async (path) => [
      path,
      await authStore.checkRouteAccess(path, getRequiredPermissions(path) || []),
    ])
  )
  routeAccess.value = Object.fromEntries(entries)
}

const handleLogout = async () => {
  await authStore.logout()
  messageUnread.value = 0
  notificationUnread.value = 0
  routeAccess.value = {}
  router.push('/login')
}

// SSE 推送驱动：收到通知/私信事件立即刷新角标；连接建立（含断线重连）时全量刷新
const unbindStreamEvents = [
  onStreamEvent('notification', loadUnreadCounts),
  onStreamEvent('message', loadUnreadCounts),
  onStreamOpen(loadUnreadCounts),
]

onMounted(() => {
  updateRouteAccess()
  loadUnreadCounts()
})

onUnmounted(() => {
  unbindStreamEvents.forEach((unbind) => unbind())
})

// 路由切换后立即刷新未读数（如在消息页处理后返回其他页面）
watch(() => route.fullPath, loadUnreadCounts)

watch(
  () => [authStore.isAuthenticated, authStore.user?.roles, authStore.user?.permissions],
  () => {
    updateRouteAccess()
  }
)
</script>

<style scoped>
.sidebar {
  position: fixed;
  left: 0;
  top: 0;
  height: 100vh;
  width: 260px;
  background: var(--surface);
  border-right: 1px solid var(--border);
  transition: width 0.3s ease;
  z-index: 1000;
  display: flex;
  flex-direction: column;
}

.sidebar.collapsed {
  width: 70px;
}

.toggle-btn {
  position: absolute;
  right: -12px;
  top: 20px;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: var(--surface);
  border: 1px solid var(--border);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
  z-index: 10;
}

.toggle-btn:hover {
  background: var(--primary);
  border-color: var(--primary);
  color: white;
}

.toggle-btn svg {
  width: 16px;
  height: 16px;
}

.sidebar-nav {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 0 0 1rem;
  overflow-y: auto;
}

.sidebar-brand {
  height: 72px;
  display: flex;
  align-items: center;
  gap: .7rem;
  padding: 0 1rem;
  color: var(--text);
  text-decoration: none;
}

.brand-mark {
  width: 36px;
  height: 36px;
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  border-radius: 10px;
  color: #fff;
  background: var(--primary);
  font-weight: 750;
}

.brand-copy { display:flex; flex-direction:column; line-height:1.2; white-space:nowrap; }
.brand-copy strong { font-size:.92rem; }
.brand-copy small { margin-top:.2rem; color:var(--text-secondary); font-size:.62rem; letter-spacing:.08em; text-transform:uppercase; }

.nav-section {
  flex: 1;
  padding: 0 0.5rem;
}

.nav-item {
  display: flex;
  align-items: center;
  padding: 0.75rem;
  margin-bottom: 0.25rem;
  border-radius: var(--radius);
  transition: all 0.2s;
  gap: 0.75rem;
  text-decoration: none;
  color: var(--text);
}

.nav-item:hover {
  background: var(--bg);
}

.nav-item.router-link-active {
  background: var(--primary);
  color: white;
}

.nav-item.router-link-active .nav-icon {
  color: white;
}

.nav-icon {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
  color: var(--text-secondary);
}

.nav-label {
  flex: 1;
  font-weight: 500;
  white-space: nowrap;
}

.nav-icon-wrap {
  position: relative;
  display: inline-flex;
  flex-shrink: 0;
}

.nav-badge {
  position: absolute;
  top: -7px;
  right: -9px;
  min-width: 16px;
  height: 16px;
  padding: 0 4px;
  border-radius: 999px;
  background: #ef4444;
  color: #fff;
  font-size: 10px;
  font-weight: 700;
  line-height: 16px;
  text-align: center;
  box-sizing: border-box;
}

.user-section {
  border-top: 1px solid var(--border);
  padding: 1rem 0.5rem;
}

.user-profile {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  border-radius: var(--radius);
  cursor: pointer;
  transition: all 0.2s;
  margin-bottom: 0.5rem;
}

.user-profile:hover {
  background: var(--bg);
}

.user-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: var(--primary);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  overflow: hidden;
}

.user-avatar img {
  width: 100%;
  height: 100%;
  border-radius: 50%;
  object-fit: cover;
}

.user-avatar svg {
  width: 24px;
  height: 24px;
}

.user-details {
  flex: 1;
  min-width: 0;
}

.user-name {
  font-weight: 600;
  color: var(--text);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.user-credit {
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.logout-btn {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  border-radius: var(--radius);
  cursor: pointer;
  transition: all 0.2s;
  font-size: 0.875rem;
}

.logout-btn:hover {
  background: var(--bg);
  color: var(--text);
}

.logout-btn svg {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}

.sidebar.collapsed .nav-item {
  justify-content: center;
  padding: 0.75rem 0.5rem;
}

.sidebar.collapsed .user-profile {
  justify-content: center;
  padding: 0.75rem 0.5rem;
}

.sidebar.collapsed .logout-btn {
  justify-content: center;
  padding: 0.75rem 0.5rem;
}
</style>
