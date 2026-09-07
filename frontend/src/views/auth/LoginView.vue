<template>
  <div class="auth-container">
    <section class="auth-visual">
      <span class="auth-brand">TONGJI · CAMPUS HUB</span>
      <div><small>WELCOME BACK</small><h2>重新连接<br><span>你的校园生活。</span></h2><p>讨论、交易与互助，都从一个可信的校园身份开始。</p></div>
      <div class="auth-proof"><span><b>校内实名</b><small>可信身份连接</small></span><span><b>一站式</b><small>社区与交易</small></span></div>
    </section>
    <div class="auth-card">
      <span class="auth-card-kicker">MEMBER LOGIN</span>
      <h1 class="auth-title">登录</h1>
      <p class="auth-subtitle">欢迎回到同济论坛</p>

      <form @submit.prevent="handleLogin" class="auth-form">
        <div v-if="error" class="error-message">{{ error }}</div>

        <div class="form-group">
          <label for="email">邮箱</label>
          <input
            id="email"
            v-model="form.email"
            type="email"
            placeholder="your@tongji.edu.cn"
            required
            :disabled="loading"
          />
        </div>

        <div class="form-group">
          <label for="password">密码</label>
          <input
            id="password"
            v-model="form.password"
            type="password"
            placeholder="请输入密码"
            required
            :disabled="loading"
          />
        </div>

        <div class="form-actions">
          <router-link to="/forgot-password" class="link">忘记密码？</router-link>
        </div>

        <button type="submit" class="btn-primary" :disabled="loading">
          {{ loading ? '登录中...' : '登录' }}
        </button>

        <div class="auth-footer">
          还没有账号？
          <router-link to="/register" class="link">立即注册</router-link>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup>
import { onBeforeUnmount, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const form = ref({
  email: '',
  password: '',
})

const loading = ref(false)
const error = ref('')

const MESSAGE_TIMEOUT_MS = 4000
let messageTimer = null

const clearMessageTimer = () => {
  if (messageTimer) {
    clearTimeout(messageTimer)
    messageTimer = null
  }
}

const clearMessages = () => {
  clearMessageTimer()
  error.value = ''
}

const showError = (message) => {
  error.value = message
  clearMessageTimer()
  messageTimer = setTimeout(() => {
    error.value = ''
    messageTimer = null
  }, MESSAGE_TIMEOUT_MS)
}

onBeforeUnmount(clearMessageTimer)

const handleLogin = async () => {
  try {
    loading.value = true
    clearMessages()

    const result = await authStore.login(form.value)
    
    if (result.success) {
      router.push('/')
    } else {
      showError(result.message || '登录失败')
    }
  } catch (err) {
    showError(err.response?.data?.message || '登录失败，请稍后重试')
  } finally {
    loading.value = false
  }
}
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

.form-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: -8px;
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
</style>
