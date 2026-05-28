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
import { useAuthStore } from './stores/auth'
import Sidebar from './components/Sidebar.vue'

const authStore = useAuthStore()
</script>

<style>
#app {
  min-height: 100vh;
}

.main-content {
  margin-left: 260px;
  padding: 2rem;
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
</style>
