<template>
  <div class="page-container profile-page">
    <header class="profile-header">
      <div>
        <p class="eyebrow">ACCOUNT SETTINGS</p>
        <h1>个人设置</h1>
        <p class="page-intro">管理你的公开资料与账号安全</p>
      </div>
      <router-link
        v-if="profile?.userId"
        :to="`/user/${profile.userId}`"
        class="profile-link"
      >
        查看我的主页
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M5 12h14" />
          <path d="m13 6 6 6-6 6" />
        </svg>
      </router-link>
    </header>

    <div v-if="loading" class="loading">加载中...</div>
    <div v-else-if="!profile" class="error-message">{{ error || '无法加载个人资料' }}</div>
    <template v-else>
      <section class="card profile-overview">
        <div class="overview-avatar">
          <img
            v-if="avatarUrl"
            :src="avatarUrl"
            :alt="profile.username || '用户头像'"
            @error="markAvatarFailed(profile.avatarUrl)"
          />
          <span v-else>{{ avatarInitial }}</span>
        </div>
        <div class="overview-copy">
          <div class="overview-title">
            <h2>{{ profile.username || '校园用户' }}</h2>
            <span class="status-pill" :class="profile.status === 'Active' ? 'is-active' : 'is-muted'">
              {{ profile.status === 'Active' ? '正常使用' : (profile.status || '状态未知') }}
            </span>
          </div>
          <p class="overview-email">{{ profile.email || '未绑定邮箱' }}</p>
          <p class="overview-bio">{{ profile.bio || '还没有个人简介，去资料维护里介绍一下自己吧。' }}</p>
        </div>
        <div class="overview-stats" aria-label="账户概览">
          <div class="overview-stat">
            <span>信用分</span>
            <strong>{{ profile.credit ?? 0 }}</strong>
          </div>
        </div>
      </section>

      <div v-if="error" class="error-message profile-message" role="alert">{{ error }}</div>
      <div v-if="success" class="success-message profile-message" role="status">{{ success }}</div>

      <section class="settings-layout">
        <form class="card settings-card" @submit.prevent="handleUpdateProfile">
          <div class="section-heading">
            <div>
              <p class="section-kicker">PUBLIC PROFILE</p>
              <h2>个人资料</h2>
            </div>
            <span class="section-note">对其他用户可见</span>
          </div>

          <div class="avatar-field">
            <div class="avatar-field-preview">
              <img
                v-if="avatarUrl"
                :src="avatarUrl"
                :alt="profile.username || '用户头像'"
                @error="markAvatarFailed(profile.avatarUrl)"
              />
              <span v-else>{{ avatarInitial }}</span>
            </div>
            <div class="avatar-field-copy">
              <strong>头像</strong>
              <span>让大家更容易认出你</span>
              <button
                class="upload-button"
                type="button"
                :disabled="avatarUploading"
                @click="openAvatarPicker"
              >
                {{ avatarUploading ? '上传中…' : '更换头像' }}
              </button>
              <input
                ref="avatarInput"
                class="avatar-upload-input"
                type="file"
                accept="image/jpeg,image/png,image/gif,image/webp"
                :disabled="avatarUploading"
                @change="handleAvatarUpload"
              />
            </div>
          </div>

          <div class="form-fields">
            <label class="field">
              <span>用户名</span>
              <input
                v-model="profileForm.username"
                type="text"
                minlength="2"
                maxlength="50"
                pattern="\S+"
                placeholder="2-50个字符，不能包含空格"
                required
              />
            </label>
            <label class="field">
              <span>联系方式 <em>可选</em></span>
              <input v-model="profileForm.contact" type="text" maxlength="100" placeholder="QQ、微信或手机号" />
            </label>
            <label class="field field-wide">
              <span>个人简介 <em>可选</em></span>
              <textarea v-model="profileForm.bio" maxlength="500" rows="4" placeholder="简单介绍一下自己"></textarea>
              <small>{{ profileForm.bio.length }}/500</small>
            </label>
          </div>

          <div class="form-footer">
            <span>用户名会显示在帖子、商品和好友列表中</span>
            <button class="btn btn-primary" type="submit" :disabled="profileSaving">
              {{ profileSaving ? '保存中…' : '保存资料' }}
            </button>
          </div>
        </form>

        <form class="card settings-card security-card" @submit.prevent="handleChangePassword">
          <div class="section-heading">
            <div>
              <p class="section-kicker">ACCOUNT SECURITY</p>
              <h2>账号安全</h2>
            </div>
            <svg class="section-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
              <rect x="4" y="10" width="16" height="11" rx="2" />
              <path d="M8 10V7a4 4 0 0 1 8 0v3" />
            </svg>
          </div>

          <div class="form-fields">
            <label class="field">
              <span>当前密码</span>
              <input v-model="passwordForm.currentPassword" type="password" autocomplete="current-password" required />
            </label>
            <label class="field">
              <span>新密码</span>
              <input v-model="passwordForm.newPassword" type="password" autocomplete="new-password" required />
            </label>
            <label class="field">
              <span>确认新密码</span>
              <input v-model="passwordForm.confirmPassword" type="password" autocomplete="new-password" required />
            </label>
          </div>
          <div class="form-footer">
            <span>修改后需要使用新密码登录</span>
            <button class="btn btn-primary" type="submit" :disabled="passwordSaving">
              {{ passwordSaving ? '提交中…' : '修改密码' }}
            </button>
          </div>
        </form>
      </section>

      <section class="card credit-card">
        <div class="section-heading credit-heading">
          <div>
            <p class="section-kicker">CREDIT HISTORY</p>
            <h2>信用流水</h2>
          </div>
          <span class="section-note">最近 50 条</span>
        </div>
        <div v-if="creditAdjustments.length === 0" class="empty-credit">暂无信用变更记录</div>
        <div v-else class="credit-list">
          <article v-for="item in creditAdjustments" :key="item.creditAdjustmentId" class="credit-item">
            <div class="credit-item-copy">
              <strong>{{ item.description || '信用变更' }}</strong>
              <span>{{ formatDate(item.adjustTime) }}</span>
              <small v-if="item.beforeCredit !== null && item.afterCredit !== null">
                {{ item.beforeCredit }} → {{ item.afterCredit }}
                <template v-if="item.operatorName"> · 操作人：{{ item.operatorName }}</template>
              </small>
            </div>
            <b :class="item.changePoints >= 0 ? 'credit-up' : 'credit-down'">
              {{ item.changePoints >= 0 ? '+' : '' }}{{ item.changePoints }}
            </b>
          </article>
        </div>
      </section>
    </template>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { changePassword, getCreditAdjustments, getProfile, updateProfile, uploadAvatar } from '../api'
import { useAuthStore } from '../stores/auth'
import { canShowAvatar, markAvatarFailed } from '../utils/avatarFallback'

const authStore = useAuthStore()
const profile = ref(null)
const creditAdjustments = ref([])
const loading = ref(true)
const avatarUploading = ref(false)
const avatarInput = ref(null)
const profileSaving = ref(false)
const passwordSaving = ref(false)
const error = ref('')
const success = ref('')

const MESSAGE_TIMEOUT_MS = 4000
let messageTimer = null

const profileForm = ref({
  username: '',
  avatarUrl: '',
  contact: '',
  bio: '',
})

const passwordForm = ref({
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
})

const avatarUrl = computed(() => {
  const url = profile.value?.avatarUrl || profileForm.value.avatarUrl || ''
  return canShowAvatar(url) ? url : ''
})

const avatarInitial = computed(() =>
  (profile.value?.username || profileForm.value.username || '校')[0]?.toUpperCase() || '校'
)

const openAvatarPicker = () => {
  avatarInput.value?.click()
}

const loadProfile = async () => {
  try {
    loading.value = true
    const [res, adjustmentsRes] = await Promise.all([getProfile(), getCreditAdjustments()])
    profile.value = res.data
    creditAdjustments.value = adjustmentsRes.data || []
    profileForm.value.username = res.data.username || ''
    profileForm.value.avatarUrl = res.data.avatarUrl || ''
    profileForm.value.contact = res.data.contact || ''
    profileForm.value.bio = res.data.bio || ''
  } catch (e) {
    showError('无法加载个人资料：' + (e.response?.data?.message || e.message))
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

const validatePassword = (password) =>
  password.length >= 8 && /[A-Z]/.test(password) && /[a-z]/.test(password) && /\d/.test(password)

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
      showError('头像文件不能超过 2MB')
      return
    }

    avatarUploading.value = true
    const res = await uploadAvatar(file)
    const nextAvatarUrl = res.data.avatarUrl || ''
    profileForm.value.avatarUrl = nextAvatarUrl
    profile.value = { ...profile.value, avatarUrl: nextAvatarUrl }
    authStore.user = { ...authStore.user, avatarUrl: nextAvatarUrl }
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
    profileSaving.value = true
    const res = await updateProfile({
      username: profileForm.value.username,
      contact: profileForm.value.contact,
      bio: profileForm.value.bio,
    })
    profile.value = { ...profile.value, ...res.data }
    authStore.user = {
      ...authStore.user,
      username: res.data.username,
      contact: res.data.contact,
      bio: res.data.bio,
    }
    showSuccess('资料已更新')
  } catch (e) {
    showError(e.response?.data?.message || '资料更新失败')
  } finally {
    profileSaving.value = false
  }
}

const handleChangePassword = async () => {
  try {
    clearMessages()
    if (!validatePassword(passwordForm.value.newPassword)) {
      showError('新密码必须至少 8 位，包含大小写字母和数字')
      return
    }
    if (passwordForm.value.newPassword !== passwordForm.value.confirmPassword) {
      showError('两次输入的新密码不一致')
      return
    }

    passwordSaving.value = true
    await changePassword({
      currentPassword: passwordForm.value.currentPassword,
      newPassword: passwordForm.value.newPassword,
    })
    passwordForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' }
    showSuccess('密码已修改')
  } catch (e) {
    showError(e.response?.data?.message || '密码修改失败')
  } finally {
    passwordSaving.value = false
  }
}

onBeforeUnmount(clearMessageTimer)
onMounted(loadProfile)
</script>

<style scoped>
.profile-page {
  width: 100%;
  max-width: 1120px;
  margin: 0 auto;
  color: #171b2d;
  padding-bottom: 3rem;
}

.profile-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1.5rem;
  padding: 0.5rem 0 1.4rem;
}

.eyebrow,
.section-kicker {
  color: #7165d8;
  font-size: 0.68rem;
  font-weight: 800;
  letter-spacing: 0.13em;
}

.profile-header h1 {
  margin-top: 0.35rem;
  font-size: clamp(1.65rem, 3vw, 2.15rem);
  letter-spacing: -0.04em;
}

.page-intro {
  margin-top: 0.35rem;
  color: #7b8195;
  font-size: 0.88rem;
}

.profile-link {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  color: #5f50dc;
  font-size: 0.82rem;
  font-weight: 700;
  text-decoration: none;
  white-space: nowrap;
}

.profile-link svg {
  width: 1rem;
  height: 1rem;
}

.card {
  border: 1px solid #e9eaf1;
  border-radius: 20px;
  box-shadow: 0 10px 30px rgba(39, 44, 75, 0.04);
}

.profile-overview {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.35rem 1.5rem;
  background: linear-gradient(115deg, #fbfbff 0%, #fff 55%, #f2fffc 100%);
}

.overview-avatar,
.avatar-field-preview {
  display: grid;
  flex: 0 0 auto;
  place-items: center;
  overflow: hidden;
  border-radius: 50%;
  color: #fff;
  background: linear-gradient(135deg, #5e50d9, #8975ee);
  font-weight: 800;
}

.overview-avatar {
  width: 72px;
  height: 72px;
  font-size: 1.65rem;
}

.overview-avatar img,
.avatar-field-preview img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.overview-copy {
  min-width: 0;
  flex: 1;
}

.overview-title {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.6rem;
}

.overview-title h2 {
  font-size: 1.2rem;
  letter-spacing: -0.025em;
}

.status-pill {
  border-radius: 999px;
  padding: 0.2rem 0.55rem;
  font-size: 0.68rem;
  font-weight: 700;
}

.status-pill.is-active {
  color: #15805c;
  background: #e3f8ef;
}

.status-pill.is-muted {
  color: #73798b;
  background: #eef0f4;
}

.overview-email {
  margin-top: 0.15rem;
  color: #70778c;
  font-size: 0.78rem;
}

.overview-bio {
  display: -webkit-box;
  overflow: hidden;
  margin-top: 0.45rem;
  color: #535a6d;
  font-size: 0.8rem;
  line-height: 1.5;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.overview-stats {
  display: flex;
  flex: 0 0 auto;
  gap: 1.8rem;
  padding-left: 1.5rem;
  border-left: 1px solid #e7e8ef;
}

.overview-stat {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  min-width: 58px;
}

.overview-stat span {
  color: #858b9d;
  font-size: 0.7rem;
}

.overview-stat strong {
  color: #292d40;
  font-size: 1.08rem;
}

.profile-message {
  margin-top: 1rem;
  margin-bottom: 0;
}

.error-message,
.success-message {
  border-radius: 12px;
  padding: 0.75rem 1rem;
  font-size: 0.82rem;
}

.error-message {
  color: #a33c4c;
  background: #fff0f2;
}

.success-message {
  color: #15724f;
  background: #e8f8f0;
}

.settings-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.25fr) minmax(300px, 0.75fr);
  gap: 1rem;
  margin-top: 1rem;
}

.settings-card,
.credit-card {
  padding: 1.35rem 1.5rem;
}

.settings-card {
  display: flex;
  flex-direction: column;
  gap: 1.1rem;
}

.section-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

.section-heading h2 {
  margin-top: 0.25rem;
  color: #21263a;
  font-size: 1rem;
  letter-spacing: -0.02em;
}

.section-note {
  padding-top: 0.15rem;
  color: #959bad;
  font-size: 0.72rem;
  white-space: nowrap;
}

.section-icon {
  width: 1.3rem;
  height: 1.3rem;
  color: #7568dc;
}

.avatar-field {
  display: flex;
  align-items: center;
  gap: 0.8rem;
  padding: 0.85rem;
  border: 1px solid #ececf3;
  border-radius: 14px;
  background: #fafaff;
}

.avatar-field-preview {
  width: 52px;
  height: 52px;
  font-size: 1.15rem;
}

.avatar-field-copy {
  display: flex;
  flex: 1;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.2rem 0.65rem;
  min-width: 0;
}

.avatar-field-copy strong {
  width: 100%;
  color: #2c3144;
  font-size: 0.82rem;
}

.avatar-field-copy > span {
  color: #8a90a1;
  font-size: 0.72rem;
}

.upload-button {
  margin-left: auto;
  padding: 0.4rem 0.7rem;
  border: 1px solid #dad8f8;
  border-radius: 8px;
  color: #5e50d9;
  background: #f3f1ff;
  cursor: pointer;
  font: inherit;
  font-size: 0.72rem;
  font-weight: 700;
}

.avatar-upload-input {
  position: absolute;
  width: 1px;
  height: 1px;
  opacity: 0;
}

.form-fields {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.85rem;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  color: #5f667a;
  font-size: 0.78rem;
}

.field-wide {
  grid-column: 1 / -1;
}

.field em {
  color: #a5aabb;
  font-size: 0.7rem;
  font-style: normal;
}

.field input,
.field textarea {
  width: 100%;
  border: 1px solid #e1e3eb;
  border-radius: 10px;
  color: #252a3d;
  background: #fff;
  font: inherit;
  padding: 0.62rem 0.72rem;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.field input::placeholder,
.field textarea::placeholder {
  color: #b0b4c1;
}

.field input:focus,
.field textarea:focus {
  border-color: #7568dc;
  outline: none;
  box-shadow: 0 0 0 3px rgba(117, 104, 220, 0.11);
}

.field textarea {
  min-height: 102px;
  resize: vertical;
}

.field small {
  align-self: flex-end;
  margin-top: -0.2rem;
  color: #a1a6b5;
  font-size: 0.68rem;
}

.form-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin-top: auto;
  padding-top: 0.2rem;
}

.form-footer > span {
  color: #9a9fae;
  font-size: 0.68rem;
  line-height: 1.4;
}

.btn {
  flex: 0 0 auto;
  border-radius: 9px;
  padding: 0.58rem 0.9rem;
  font-size: 0.78rem;
}

.btn:disabled,
.upload-button:disabled {
  cursor: not-allowed;
  opacity: 0.62;
}

.btn-primary {
  background: #5e50d9;
}

.btn-primary:hover:not(:disabled) {
  background: #4e41c6;
}

.security-card {
  background: linear-gradient(160deg, #fff 62%, #f8f6ff);
}

.security-card .form-fields {
  grid-template-columns: 1fr;
}

.credit-card {
  margin-top: 1rem;
}

.credit-heading {
  align-items: center;
}

.empty-credit {
  padding: 1.6rem 0 0.4rem;
  color: #9ba0ae;
  font-size: 0.78rem;
  text-align: center;
}

.credit-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.55rem;
  margin-top: 1rem;
}

.credit-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.72rem 0.8rem;
  border-radius: 11px;
  background: #fafafd;
}

.credit-item-copy {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 0.15rem;
}

.credit-item strong {
  overflow: hidden;
  color: #34394b;
  font-size: 0.76rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.credit-item span,
.credit-item small {
  color: #979cab;
  font-size: 0.67rem;
}

.credit-item small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.credit-item b {
  font-size: 0.86rem;
  white-space: nowrap;
}

.credit-up {
  color: #16805a;
}

.credit-down {
  color: #d15b67;
}

@media (max-width: 820px) {
  .settings-layout {
    grid-template-columns: 1fr;
  }

  .security-card .form-fields {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 620px) {
  .profile-header {
    align-items: flex-start;
    flex-direction: column;
    gap: 0.75rem;
  }

  .profile-overview {
    align-items: flex-start;
    flex-wrap: wrap;
  }

  .overview-copy {
    flex-basis: calc(100% - 88px);
  }

  .overview-stats {
    width: 100%;
    justify-content: space-around;
    padding: 0.9rem 0 0;
    border-top: 1px solid #e7e8ef;
    border-left: 0;
  }

  .overview-stat {
    align-items: center;
  }

  .settings-card,
  .credit-card {
    padding: 1.1rem;
  }

  .form-fields,
  .security-card .form-fields,
  .credit-list {
    grid-template-columns: 1fr;
  }

  .field-wide {
    grid-column: auto;
  }

  .form-footer {
    align-items: flex-start;
    flex-direction: column;
  }

  .form-footer .btn {
    width: 100%;
  }

  .upload-button {
    margin-left: 0;
  }
}
</style>
