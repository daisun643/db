<template>
  <div id="app">
    <template v-if="authStore.isAuthenticated">
      <Sidebar />
      <main class="main-content">
        <router-view />
      </main>
    </template>
    <template v-else>
      <main class="auth-container">
        <router-view />
      </main>
    </template>
  </div>
</template>

<script setup>
import { watch } from 'vue'
import { useAuthStore } from './stores/auth'
import Sidebar from './components/Sidebar.vue'
import { startStream, stopStream } from './utils/notificationStream'

const authStore = useAuthStore()

// 登录后建立 SSE 推送连接，登出时关闭；immediate 覆盖页面刷新时已登录的场景
watch(
  () => authStore.isAuthenticated,
  (authenticated) => (authenticated ? startStream() : stopStream()),
  { immediate: true },
)
</script>

<style>
#app {
  min-height: 100vh;
}

.main-content {
  margin-left: 260px;
  padding: 2rem clamp(1.5rem, 3vw, 3rem);
  transition: margin-left 0.3s ease;
  background: var(--bg);
  min-height: 100vh;
}

.sidebar.collapsed ~ .main-content {
  margin-left: 70px;
}

.auth-container {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg);
}

@media (max-width: 900px) {
  .main-content,
  .sidebar.collapsed ~ .main-content {
    margin-left: 70px;
    padding: 1rem;
  }
}
</style>
