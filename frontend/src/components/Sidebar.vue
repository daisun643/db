<template>
  <aside :class="['sidebar', { collapsed: isCollapsed }]">
    <button class="toggle-btn" @click="toggleSidebar" aria-label="切换侧边栏">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M15 18l-6-6 6-6" v-if="!isCollapsed" />
        <path d="M9 18l6-6-6-6" v-else />
      </svg>
    </button>

    <nav class="sidebar-nav">
      <div class="nav-section">
        <router-link to="/" class="nav-item">
          <svg class="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
            <polyline points="9 22 9 12 15 12 15 22" />
          </svg>
          <span class="nav-label" v-if="!isCollapsed">首页</span>
        </router-link>

        <router-link to="/forums" class="nav-item">
          <svg class="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
          </svg>
          <span class="nav-label" v-if="!isCollapsed">论坛</span>
        </router-link>

        <router-link to="/products" class="nav-item">
          <svg class="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="9" cy="21" r="1" />
            <circle cx="20" cy="21" r="1" />
            <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6" />
          </svg>
          <span class="nav-label" v-if="!isCollapsed">交易</span>
        </router-link>

        <router-link to="/messages" class="nav-item">
          <svg class="nav-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" />
            <polyline points="22,6 12,13 2,6" />
          </svg>
          <span class="nav-label" v-if="!isCollapsed">消息</span>
        </router-link>
      </div>

      <div class="user-section">
        <div class="user-profile" @click="$router.push('/profile')">
          <div class="user-avatar">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
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
import { ref } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useRouter } from 'vue-router'

const authStore = useAuthStore()
const router = useRouter()

const isCollapsed = ref(false)

const toggleSidebar = () => {
  isCollapsed.value = !isCollapsed.value
}

const handleLogout = async () => {
  await authStore.logout()
  router.push('/login')
}
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
  padding: 1rem 0;
  overflow-y: auto;
}

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
