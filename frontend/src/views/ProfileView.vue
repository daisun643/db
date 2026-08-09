<template>
  <div class="page-container profile-page">
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
            <span class="info-label">用户名</span>
            <span class="info-value">{{ profile?.username || '-' }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">昵称</span>
            <span class="info-value">{{ profile?.nickname || '-' }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">联系方式</span>
            <span class="info-value">{{ profile?.contact || '-' }}</span>
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
        <div v-if="error" class="error-message profile-message">{{ error }}</div>
        <div v-if="success" class="success-message profile-message">{{ success }}</div>
      </div>

      <div class="settings-grid">
        <form class="card settings-card" @submit.prevent="handleUpdateProfile">
          <h3>资料维护</h3>
          <label>
            <span>用户名</span>
            <input v-model="profileForm.username" type="text" minlength="2" maxlength="50" required />
          </label>
          <label>
            <span>昵称</span>
            <input v-model="profileForm.nickname" type="text" maxlength="50" placeholder="展示给其他用户的名称" />
          </label>
          <label>
            <span>头像</span>
            <input
              type="file"
              accept="image/jpeg,image/png,image/gif,image/webp"
              :disabled="avatarUploading"
              @change="handleAvatarUpload"
            />
            <small class="field-hint">支持 JPG、PNG、GIF、WebP，文件不超过 2MB</small>
          </label>
          <label>
            <span>联系方式</span>
            <input v-model="profileForm.contact" type="text" maxlength="100" placeholder="QQ / 微信 / 手机号等" />
          </label>
          <label>
            <span>个人简介</span>
            <textarea v-model="profileForm.bio" maxlength="500" rows="4" placeholder="简单介绍一下自己"></textarea>
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
              <small v-if="item.beforeCredit !== null && item.afterCredit !== null">
                {{ item.beforeCredit }} -> {{ item.afterCredit }}
                <template v-if="item.operatorName"> · 操作人：{{ item.operatorName }}</template>
              </small>
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
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { changePassword, getCreditAdjustments, getProfile, updateProfile, uploadAvatar } from '../api'
import { useAuthStore } from '../stores/auth'

const authStore = useAuthStore()
const profile = ref(null)
const creditAdjustments = ref([])
const loading = ref(true)
const avatarUploading = ref(false)
const error = ref('')
const success = ref('')

const MESSAGE_TIMEOUT_MS = 4000
let messageTimer = null

const profileForm = ref({
  username: '',
  nickname: '',
  avatarUrl: '',
  contact: '',
  bio: '',
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
    profileForm.value.nickname = res.data.nickname || ''
    profileForm.value.avatarUrl = res.data.avatarUrl || ''
    profileForm.value.contact = res.data.contact || ''
    profileForm.value.bio = res.data.bio || ''
  } catch (e) {
    showError('无法加载个人资料: ' + (e.response?.data?.message || e.message))
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
  clearMessageTimer()
  error.value = ''
  success.value = ''
}

const clearMessageTimer = () => {
  if (messageTimer) {
    clearTimeout(messageTimer)
    messageTimer = null
  }
}

const scheduleClearMessages = () => {
  clearMessageTimer()
  messageTimer = setTimeout(() => {
    error.value = ''
    success.value = ''
    messageTimer = null
  }, MESSAGE_TIMEOUT_MS)
}

const showError = (message) => {
  error.value = message
  success.value = ''
  scheduleClearMessages()
}

const showSuccess = (message) => {
  success.value = message
  error.value = ''
  scheduleClearMessages()
}

const handleAvatarUpload = async (event) => {
  const file = event.target.files?.[0]
  if (!file) return

  try {
    clearMessages()
    if (!['image/jpeg', 'image/png', 'image/gif', 'image/webp'].includes(file.type)) {
      showError('仅支持 JPG、PNG、GIF、WebP 图片')
      return
    }
    if (file.size > 2 * 1024 * 1024) {
      showError('头像文件不能超过2MB')
      return
    }

    avatarUploading.value = true
    const res = await uploadAvatar(file)
    profileForm.value.avatarUrl = res.data.avatarUrl
    profile.value = { ...profile.value, avatarUrl: res.data.avatarUrl }
    authStore.user = {
      ...authStore.user,
      avatarUrl: res.data.avatarUrl,
    }
    showSuccess(res.data.message || '头像已上传')
  } catch (e) {
    showError(e.response?.data?.message || '头像上传失败')
  } finally {
    avatarUploading.value = false
    event.target.value = ''
  }
}

const handleUpdateProfile = async () => {
  try {
    clearMessages()
    const res = await updateProfile({
      username: profileForm.value.username,
      nickname: profileForm.value.nickname,
      contact: profileForm.value.contact,
      bio: profileForm.value.bio,
    })
    profile.value = { ...profile.value, ...res.data }
    authStore.user = {
      ...authStore.user,
      username: res.data.username,
      nickname: res.data.nickname,
      contact: res.data.contact,
      bio: res.data.bio,
    }
    showSuccess('资料已更新')
  } catch (e) {
    showError(e.response?.data?.message || '资料更新失败')
  }
}

const handleChangePassword = async () => {
  try {
    clearMessages()
    if (!validatePassword(passwordForm.value.newPassword)) {
      showError('新密码必须至少8位，包含大小写字母和数字')
      return
    }
    if (passwordForm.value.newPassword !== passwordForm.value.confirmPassword) {
      showError('两次输入的新密码不一致')
      return
    }

    await changePassword({
      currentPassword: passwordForm.value.currentPassword,
      newPassword: passwordForm.value.newPassword,
    })
    passwordForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' }
    showSuccess('密码已修改')
  } catch (e) {
    showError(e.response?.data?.message || '密码修改失败')
  }
}

onBeforeUnmount(clearMessageTimer)
onMounted(loadProfile)
</script>

<style scoped>
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

.settings-card input,
.settings-card textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text);
  font: inherit;
  padding: 0.625rem 0.75rem;
}

.settings-card textarea {
  resize: vertical;
}

.field-hint {
  color: var(--text-secondary);
  font-size: 0.75rem;
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

.credit-item small {
  color: var(--text-secondary);
  display: block;
  font-size: 0.75rem;
  margin-top: 0.2rem;
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

.profile-message {
  margin-bottom: 0;
  margin-top: 1rem;
}
</style>

<style scoped>
.profile-page { width:100%; max-width:1480px; margin:0 auto; color:#11172a; }
.profile-page > .loading { margin-top:1rem; }
.profile-page > template + * { margin-top:1rem; }
.profile-page .card { padding:1.4rem; border:1px solid rgba(25,34,59,.08); border-radius:22px; box-shadow:0 12px 34px rgba(29,35,58,.05); }
.profile-page .card:first-of-type { margin-top:1.25rem; }
.profile-page .card h3 { font-size:1.08rem; letter-spacing:-.02em; }
.profile-page .info-grid { grid-template-columns:repeat(auto-fit,minmax(160px,1fr)); }
.profile-page .info-item { padding:.9rem; border-radius:14px; background:#f8f8fc; }
.profile-page .info-label { color:#8a90a1; font-size:.72rem; }
.profile-page .info-value { color:#252b3f; }
.profile-page .settings-grid { gap:1rem; }
.profile-page .settings-card { position:relative; overflow:hidden; }
.profile-page .settings-card:first-child { background:linear-gradient(145deg,#fff 60%,#f0fffd); }
.profile-page .settings-card:last-child { background:linear-gradient(145deg,#fff 60%,#f3f0ff); }
.profile-page .settings-card input,.profile-page .settings-card textarea { border:1px solid #e1e3eb; border-radius:11px; background:rgba(255,255,255,.9); }
.profile-page .settings-card :is(input,textarea):focus { border-color:#7463ee; outline:none; box-shadow:0 0 0 4px rgba(105,87,245,.1); }
.profile-page .settings-card .btn-primary { align-self:flex-start; border-radius:11px; background:linear-gradient(135deg,#5f50dc,#7563ef); }
.profile-page .credit-item { border:0; border-radius:14px; background:#f8f8fc; }
.profile-page .chip { color:#5d4dd7; background:#efedff; }
.profile-page .permission-item { border:1px solid #eceafc; border-radius:12px; color:#5f56a8; background:#faf9ff; }
@media(max-width:640px){.profile-page .card{padding:1rem;border-radius:18px}.profile-page .info-grid{grid-template-columns:repeat(2,minmax(0,1fr));gap:.6rem}.profile-page .info-item{padding:.7rem}.profile-page .settings-grid{grid-template-columns:1fr}.profile-page .credit-item{align-items:flex-start}}
</style>
