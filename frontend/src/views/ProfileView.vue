<template>
  <div class="page-container">
    <h1 class="page-title">个人资料</h1>

    <div v-if="error" class="error-message">{{ error }}</div>
    <div v-if="success" class="success-message">{{ success }}</div>

    <div class="card">
      <div class="profile-header">
        <div class="profile-avatar">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
            <circle cx="12" cy="7" r="4" />
          </svg>
        </div>
        <div class="profile-info">
          <h2>{{ profile?.username || '用户' }}</h2>
          <p class="profile-email">{{ profile?.email }}</p>
        </div>
      </div>
    </div>

    <div v-if="loading" class="loading">加载中...</div>
    <template v-else>
      <div class="card">
        <h3>基本信息</h3>
        <div class="info-grid">
          <div class="info-item">
            <span class="info-label">用户 ID</span>
            <span class="info-value">{{ profile?.userId || '-' }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">信用分</span>
            <span class="info-value">{{ profile?.credit ?? 0 }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">等级积分</span>
            <span class="info-value">Lv.{{ profile?.userLevel || 1 }} / {{ profile?.totalCredit || 0 }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">账号状态</span>
            <span :class="['badge', profile?.status === 'Active' ? 'badge-green' : 'badge-yellow']">
              {{ profile?.status || '未知' }}
            </span>
          </div>
        </div>
      </div>

      <div class="settings-grid">
        <form class="card settings-card" @submit.prevent="handleUpdateProfile">
          <h3>资料维护</h3>
          <label>
            <span>用户名</span>
            <input v-model="profileForm.username" type="text" minlength="2" maxlength="50" required />
          </label>
          <button class="btn btn-primary" type="submit">保存资料</button>
        </form>

        <form class="card settings-card" @submit.prevent="handleChangePassword">
          <h3>修改密码</h3>
          <label>
            <span>当前密码</span>
            <input v-model="passwordForm.currentPassword" type="password" required />
          </label>
          <label>
            <span>新密码</span>
            <input v-model="passwordForm.newPassword" type="password" required />
          </label>
          <label>
            <span>确认新密码</span>
            <input v-model="passwordForm.confirmPassword" type="password" required />
          </label>
          <button class="btn btn-primary" type="submit">修改密码</button>
        </form>
      </div>

      <div class="card">
        <h3>信用流水</h3>
        <div v-if="creditAdjustments.length === 0" class="muted">暂无信用变更记录</div>
        <div v-else class="credit-list">
          <article v-for="item in creditAdjustments" :key="item.creditAdjustmentId" class="credit-item">
            <div>
              <strong>{{ item.description || '信用变更' }}</strong>
              <span>{{ formatDate(item.adjustTime) }}</span>
            </div>
            <b :class="item.changePoints >= 0 ? 'credit-up' : 'credit-down'">
              {{ item.changePoints >= 0 ? '+' : '' }}{{ item.changePoints }}
            </b>
          </article>
        </div>
      </div>

      <div class="card">
        <h3>角色与权限</h3>
        <div class="chip-section">
          <span v-for="role in profile?.roles || []" :key="role.roleId" class="chip">
            {{ role.roleName }}
          </span>
          <span v-if="!profile?.roles?.length" class="muted">暂无角色</span>
        </div>
        <div class="permission-grid">
          <span v-for="permission in profile?.permissions || []" :key="permission.permissionId" class="permission-item">
            {{ permission.permissionName }}
          </span>
          <span v-if="!profile?.permissions?.length" class="muted">暂无权限</span>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { changePassword, getCreditAdjustments, getProfile, updateProfile } from '../api'
import { useAuthStore } from '../stores/auth'

const authStore = useAuthStore()
const profile = ref(null)
const creditAdjustments = ref([])
const loading = ref(true)
const error = ref('')
const success = ref('')

const profileForm = ref({
  username: '',
})

const passwordForm = ref({
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
})

const loadProfile = async () => {
  try {
    loading.value = true
    const [res, adjustmentsRes] = await Promise.all([getProfile(), getCreditAdjustments()])
    profile.value = res.data
    creditAdjustments.value = adjustmentsRes.data
    profileForm.value.username = res.data.username || ''
  } catch (e) {
    error.value = '无法加载个人资料: ' + (e.response?.data?.message || e.message)
  } finally {
    loading.value = false
  }
}

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

const validatePassword = (password) => {
  return password.length >= 8 && /[A-Z]/.test(password) && /[a-z]/.test(password) && /\d/.test(password)
}

const clearMessages = () => {
  error.value = ''
  success.value = ''
}

const handleUpdateProfile = async () => {
  try {
    clearMessages()
    const res = await updateProfile({ username: profileForm.value.username })
    profile.value = { ...profile.value, ...res.data }
    authStore.user = {
      ...authStore.user,
      username: res.data.username,
    }
    success.value = '资料已更新'
  } catch (e) {
    error.value = e.response?.data?.message || '资料更新失败'
  }
}

const handleChangePassword = async () => {
  try {
    clearMessages()
    if (!validatePassword(passwordForm.value.newPassword)) {
      error.value = '新密码必须至少8位，包含大小写字母和数字'
      return
    }
    if (passwordForm.value.newPassword !== passwordForm.value.confirmPassword) {
      error.value = '两次输入的新密码不一致'
      return
    }

    await changePassword({
      currentPassword: passwordForm.value.currentPassword,
      newPassword: passwordForm.value.newPassword,
    })
    passwordForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' }
    success.value = '密码已修改'
  } catch (e) {
    error.value = e.response?.data?.message || '密码修改失败'
  }
}

onMounted(loadProfile)
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
  margin-bottom: 0.25rem;
}

.profile-email,
.muted {
  color: var(--text-secondary);
}

.info-grid,
.settings-grid,
.permission-grid {
  display: grid;
  gap: 1rem;
}

.info-grid {
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  margin-top: 1rem;
}

.settings-grid {
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  margin: 1rem 0;
}

.settings-card {
  display: flex;
  flex-direction: column;
  gap: 0.875rem;
}

.settings-card label {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.settings-card input {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text);
  font: inherit;
  padding: 0.625rem 0.75rem;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.info-label {
  font-size: 0.875rem;
  color: var(--text-secondary);
}

.info-value {
  font-weight: 600;
}

.chip-section,
.permission-grid {
  margin-top: 1rem;
}

.chip-section {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.chip,
.permission-item {
  border-radius: 9999px;
  background: var(--bg);
  color: var(--primary);
  font-size: 0.75rem;
  font-weight: 600;
  padding: 0.25rem 0.75rem;
}

.permission-grid {
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
}

.credit-list {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
  margin-top: 1rem;
}

.credit-item {
  align-items: center;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.75rem;
}

.credit-item div {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.credit-item span {
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.credit-up {
  color: #15803d;
}

.credit-down {
  color: #dc2626;
}

.success-message {
  background: #dcfce7;
  color: #166534;
  padding: 1rem;
  border-radius: var(--radius);
  margin-bottom: 1rem;
}
</style>
