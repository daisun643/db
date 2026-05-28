<template>
  <div class="page-container">
    <h1 class="page-title">个人资料</h1>

    <div class="card">
      <div class="profile-header">
        <div class="profile-avatar">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
            <circle cx="12" cy="7" r="4" />
          </svg>
        </div>
        <div class="profile-info">
          <h2>{{ authStore.user?.username || '用户' }}</h2>
          <p class="profile-email">{{ authStore.user?.email }}</p>
        </div>
      </div>
    </div>

    <div class="card">
      <h3 style="margin-bottom: 1rem;">基本信息</h3>
      <div class="info-grid">
        <div class="info-item">
          <span class="info-label">用户代码</span>
          <span class="info-value">{{ authStore.user?.userCode || '-' }}</span>
        </div>
        <div class="info-item">
          <span class="info-label">信用分</span>
          <span class="info-value">{{ authStore.user?.credit || 0 }}</span>
        </div>
        <div class="info-item">
          <span class="info-label">账号状态</span>
          <span :class="['badge', authStore.user?.status === 'Active' ? 'badge-green' : 'badge-yellow']">
            {{ authStore.user?.status || '未知' }}
          </span>
        </div>
        <div class="info-item">
          <span class="info-label">邮箱</span>
          <span class="info-value">{{ authStore.user?.email || '-' }}</span>
        </div>
      </div>
    </div>

    <div class="card">
      <h3 style="margin-bottom: 1rem;">账号设置</h3>
      <div class="settings-list">
        <button class="setting-item" @click="showChangePassword = true">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
            <path d="M7 11V7a5 5 0 0 1 10 0v4" />
          </svg>
          <span>修改密码</span>
        </button>
        <button class="setting-item">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
          </svg>
          <span>隐私设置</span>
        </button>
        <button class="setting-item">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
            <path d="M13.73 21a2 2 0 0 1-3.46 0" />
          </svg>
          <span>通知设置</span>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useAuthStore } from '../stores/auth'

const authStore = useAuthStore()
const showChangePassword = ref(false)
</script>

<style scoped>
.profile-header {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  padding: 1rem 0;
}

.profile-avatar {
  width: 80px;
  height: 80px;
  border-radius: 50%;
  background: var(--primary);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.profile-avatar svg {
  width: 48px;
  height: 48px;
}

.profile-info h2 {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--text);
  margin-bottom: 0.25rem;
}

.profile-email {
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1.5rem;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.info-label {
  font-size: 0.875rem;
  color: var(--text-secondary);
  font-weight: 500;
}

.info-value {
  font-size: 1rem;
  color: var(--text);
  font-weight: 600;
}

.settings-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.setting-item {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background: transparent;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  cursor: pointer;
  transition: all 0.2s;
  text-align: left;
  font-size: 0.875rem;
  color: var(--text);
}

.setting-item:hover {
  background: var(--bg);
  border-color: var(--primary);
}

.setting-item svg {
  width: 20px;
  height: 20px;
  color: var(--text-secondary);
  flex-shrink: 0;
}

.setting-item span {
  flex: 1;
}
</style>
