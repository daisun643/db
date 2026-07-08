<template>
  <div class="auth-container">
    <div class="auth-card">
      <h1 class="auth-title">找回密码</h1>
      <p class="auth-subtitle">重置您的账号密码</p>

      <form @submit.prevent="handleResetPassword" class="auth-form">
        <div v-if="error" class="error-message">{{ error }}</div>
        <div v-if="success" class="success-message">{{ success }}</div>

        <div class="form-group">
          <label for="email">邮箱</label>
          <div class="input-with-button">
            <input
              id="email"
              v-model="form.email"
              type="email"
              placeholder="your@tongji.edu.cn"
              required
              :disabled="loading"
            />
            <button
              type="button"
              class="btn-code"
              @click="handleSendCode"
              :disabled="loading || countdown > 0"
            >
              {{ countdown > 0 ? `${countdown}秒` : '发送验证码' }}
            </button>
          </div>
        </div>

        <div class="form-group">
          <label for="code">验证码</label>
          <input
            id="code"
            v-model="form.code"
            type="text"
            placeholder="请输入6位验证码"
            maxlength="6"
            required
            :disabled="loading"
          />
        </div>

        <div class="form-group">
          <label for="newPassword">新密码</label>
          <input
            id="newPassword"
            v-model="form.newPassword"
            type="password"
            placeholder="至少8位，包含大小写字母和数字"
            required
            :disabled="loading"
          />
        </div>

        <div class="form-group">
          <label for="confirmPassword">确认新密码</label>
          <input
            id="confirmPassword"
            v-model="form.confirmPassword"
            type="password"
            placeholder="请再次输入新密码"
            required
            :disabled="loading"
          />
        </div>

        <button type="submit" class="btn-primary" :disabled="loading">
          {{ loading ? '重置中...' : '重置密码' }}
        </button>

        <div class="auth-footer">
          想起密码了？
          <router-link to="/login" class="link">返回登录</router-link>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup>
import { onBeforeUnmount, ref } from 'vue'
import { useRouter } from 'vue-router'
import { forgotPassword, resetPassword } from '../api'

const router = useRouter()

const form = ref({
  email: '',
  code: '',
  newPassword: '',
  confirmPassword: '',
})

const loading = ref(false)
const error = ref('')
const success = ref('')
const countdown = ref(0)

const MESSAGE_TIMEOUT_MS = 4000
let messageTimer = null

const validatePassword = (password) => {
  if (password.length < 8) return false
  const hasUpper = /[A-Z]/.test(password)
  const hasLower = /[a-z]/.test(password)
  const hasDigit = /\d/.test(password)
  return hasUpper && hasLower && hasDigit
}

const clearMessageTimer = () => {
  if (messageTimer) {
    clearTimeout(messageTimer)
    messageTimer = null
  }
}

const clearMessages = () => {
  clearMessageTimer()
  error.value = ''
  success.value = ''
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

const handleSendCode = async () => {
  try {
    clearMessages()

    if (!form.value.email) {
      showError('请输入邮箱')
      return
    }

    loading.value = true
    const response = await forgotPassword(form.value.email)

    if (response.data.success) {
      showSuccess('验证码已发送，请查收邮件')
      countdown.value = 60
      const timer = setInterval(() => {
        countdown.value--
        if (countdown.value <= 0) {
          clearInterval(timer)
        }
      }, 1000)
    } else {
      showError(response.data.message || '发送失败')
    }
  } catch (err) {
    showError(err.response?.data?.message || '发送失败，请稍后重试')
  } finally {
    loading.value = false
  }
}

const handleResetPassword = async () => {
  try {
    loading.value = true
    clearMessages()

    if (!validatePassword(form.value.newPassword)) {
      showError('密码必须至少8位，包含大小写字母和数字')
      return
    }

    if (form.value.newPassword !== form.value.confirmPassword) {
      showError('两次输入的密码不一致')
      return
    }

    const response = await resetPassword({
      email: form.value.email,
      code: form.value.code,
      newPassword: form.value.newPassword,
    })

    if (response.data.success) {
      showSuccess('密码重置成功，3秒后跳转到登录页')
      setTimeout(() => {
        router.push('/login')
      }, 3000)
    } else {
      showError(response.data.message || '重置失败')
    }
  } catch (err) {
    showError(err.response?.data?.message || '重置失败，请稍后重试')
  } finally {
    loading.value = false
  }
}

onBeforeUnmount(clearMessageTimer)
</script>

<style scoped>
.auth-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 20px;
}

.auth-card {
  background: white;
  border-radius: 16px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  padding: 48px;
  width: 100%;
  max-width: 440px;
}

.auth-title {
  font-size: 32px;
  font-weight: 700;
  color: #1e293b;
  margin-bottom: 8px;
  text-align: center;
}

.auth-subtitle {
  color: #64748b;
  text-align: center;
  margin-bottom: 32px;
}

.auth-form {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-group label {
  font-weight: 600;
  color: #334155;
  font-size: 14px;
}

.form-group input {
  padding: 12px 16px;
  border: 2px solid #e2e8f0;
  border-radius: 8px;
  font-size: 16px;
  transition: all 0.2s;
}

.form-group input:focus {
  outline: none;
  border-color: #667eea;
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
}

.form-group input:disabled {
  background: #f1f5f9;
  cursor: not-allowed;
}

.input-with-button {
  display: flex;
  gap: 8px;
}

.input-with-button input {
  flex: 1;
}

.btn-code {
  padding: 12px 20px;
  background: white;
  border: 2px solid #667eea;
  color: #667eea;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
  transition: all 0.2s;
}

.btn-code:hover:not(:disabled) {
  background: #667eea;
  color: white;
}

.btn-code:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-primary {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  padding: 14px;
  border-radius: 8px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: transform 0.2s, box-shadow 0.2s;
}

.btn-primary:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 10px 20px rgba(102, 126, 234, 0.3);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.auth-footer {
  text-align: center;
  color: #64748b;
  font-size: 14px;
}

.link {
  color: #667eea;
  text-decoration: none;
  font-weight: 600;
}

.link:hover {
  text-decoration: underline;
}

.error-message {
  background: #fee2e2;
  color: #991b1b;
  padding: 12px 16px;
  border-radius: 8px;
  font-size: 14px;
  border-left: 4px solid #dc2626;
}

.success-message {
  background: #dcfce7;
  color: #166534;
  padding: 12px 16px;
  border-radius: 8px;
  font-size: 14px;
  border-left: 4px solid #16a34a;
}
</style>
