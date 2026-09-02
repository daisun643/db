<template>
  <div class="page-container system-admin-page">
    <AdminPageHeader title="用户管理" eyebrow="用户治理" description="创建用户并调整信用分。" />
    <div class="card">
      <h2>用户管理</h2>
      <div v-if="message" :class="[`${message.type}-message`, 'section-message']">{{ message.text }}</div>
      <form class="user-create-form" @submit.prevent="handleCreateUser">
        <input v-model="userForm.username" type="text" placeholder="用户名" required />
        <input v-model="userForm.email" type="email" placeholder="校园邮箱" required />
        <input v-model="userForm.password" type="password" placeholder="初始密码" required />
        <button class="btn btn-primary" type="submit">创建用户</button>
      </form>
      <div v-if="loadingUsers" class="loading">加载中...</div>
      <div v-else class="table-wrapper">
        <table>
          <thead>
            <tr><th>ID</th><th>用户名</th><th>邮箱</th><th>信用分</th><th>状态</th><th>角色</th></tr>
          </thead>
          <tbody>
            <tr v-for="user in users" :key="user.userID">
              <td>{{ user.userID }}</td>
              <td>{{ user.username }}</td>
              <td>{{ user.email }}</td>
              <td>
                <div class="credit-cell">
                  <strong>{{ user.credit }}</strong>
                  <div class="credit-actions">
                    <button class="btn credit-mini-button" type="button" @click="openCreditDialog(user)">调整</button>
                    <button class="btn credit-mini-button" type="button" @click="openCreditHistoryDialog(user)">记录</button>
                  </div>
                </div>
              </td>
              <td>
                <span :class="['badge', user.status === 'Active' ? 'badge-green' : 'badge-yellow']">
                  {{ user.status || '未知' }}
                </span>
              </td>
              <td>
                <span v-for="role in user.roles || []" :key="role" class="badge badge-green">{{ role }}</span>
                <span v-if="!user.roles?.length" class="muted">暂无角色</span>
              </td>
            </tr>
            <tr v-if="users.length === 0"><td colspan="6" class="empty-cell">暂无数据</td></tr>
          </tbody>
        </table>
      </div>
    </div>

    <ModalDialog
      :visible="!!selectedCreditUser"
      variant="dialog"
      title="调整信用分"
      :subtitle="selectedCreditUser ? `${selectedCreditUser.username} · 当前信用分 ${selectedCreditUser.credit}` : ''"
      tag="form"
      @close="closeCreditDialog"
      @submit.prevent="handleAdjustCredit(selectedCreditUser.userID)"
    >
      <label class="credit-form-label">
        调整分值
        <input
          v-model.number="creditDrafts[selectedCreditUser.userID].credit"
          type="number"
          min="-1000"
          max="1000"
          placeholder="正数加分，负数扣分"
          required
        />
      </label>
      <label class="credit-form-label">
        调整原因
        <textarea
          v-model="creditDrafts[selectedCreditUser.userID].reason"
          class="credit-form-textarea"
          maxlength="500"
          placeholder="请填写本次调整原因"
          required
        />
      </label>
      <template #actions>
        <button class="btn" type="button" @click="closeCreditDialog">取消</button>
        <button class="btn btn-primary" type="submit">确认调整</button>
      </template>
    </ModalDialog>

    <ModalDialog
      :visible="!!selectedCreditHistoryUser"
      variant="dialog"
      title="信用分调整记录"
      :subtitle="selectedCreditHistoryUser ? `${selectedCreditHistoryUser.username} · 当前信用分 ${selectedCreditHistoryUser.credit}` : ''"
      dialog-class="credit-history-dialog"
      @close="closeCreditHistoryDialog"
    >
      <div v-if="loadingCreditHistory" class="loading">加载中...</div>
      <div v-else-if="creditHistoryRecords.length === 0" class="muted">暂无信用分调整记录</div>
      <div v-else class="credit-history-list">
        <article v-for="record in creditHistoryRecords" :key="record.creditAdjustmentId" class="credit-history-item">
          <div>
            <strong>{{ record.description || '信用分调整' }}</strong>
            <small>
              {{ formatDate(record.adjustTime) }}
              <template v-if="record.operatorName"> · 操作人：{{ record.operatorName }}</template>
            </small>
            <small v-if="record.beforeCredit != null && record.afterCredit != null">
              {{ record.beforeCredit }} -> {{ record.afterCredit }}
            </small>
          </div>
          <b :class="record.changePoints >= 0 ? 'credit-up' : 'credit-down'">
            {{ record.changePoints >= 0 ? '+' : '' }}{{ record.changePoints }}
          </b>
        </article>
      </div>
    </ModalDialog>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AdminPageHeader from '../../components/system/AdminPageHeader.vue'
import ModalDialog from '../../components/ModalDialog.vue'
import { adjustCredit, createUser, getUserCreditAdjustments, getUsers } from '../../api'
import { useTimedMessage } from '../../composables/useTimedMessage'
import './systemAdmin.css'

const users = ref([])
const userForm = ref({ username: '', email: '', password: '' })
const creditDrafts = ref({})
const selectedCreditUser = ref(null)
const selectedCreditHistoryUser = ref(null)
const creditHistoryRecords = ref([])
const loadingCreditHistory = ref(false)
const loadingUsers = ref(true)
const { message, clearMessage, showMessage } = useTimedMessage()

const formatDate = (value) => value ? new Date(value).toLocaleString('zh-CN', {
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
}) : ''

const syncCreditDrafts = () => {
  creditDrafts.value = Object.fromEntries(users.value.map(user => [
    user.userID,
    creditDrafts.value[user.userID] || { credit: 0, reason: '' },
  ]))
}

const loadUsers = async () => {
  const res = await getUsers()
  users.value = res.data
  syncCreditDrafts()
}

const handleCreateUser = async () => {
  try {
    clearMessage()
    await createUser({
      username: userForm.value.username.trim(),
      email: userForm.value.email.trim(),
      password: userForm.value.password,
    })
    userForm.value = { username: '', email: '', password: '' }
    await loadUsers()
    showMessage('success', '用户已创建')
  } catch (error) {
    showMessage('error', error.response?.data?.message || '用户创建失败')
  }
}

const openCreditDialog = (user) => {
  selectedCreditUser.value = user
  creditDrafts.value = {
    ...creditDrafts.value,
    [user.userID]: creditDrafts.value[user.userID] || { credit: 0, reason: '' },
  }
}

const closeCreditDialog = () => {
  selectedCreditUser.value = null
}

const openCreditHistoryDialog = async (user) => {
  selectedCreditHistoryUser.value = user
  creditHistoryRecords.value = []
  loadingCreditHistory.value = true
  try {
    clearMessage()
    const res = await getUserCreditAdjustments(user.userID)
    creditHistoryRecords.value = res.data
  } catch (error) {
    showMessage('error', error.response?.data?.message || '信用分记录加载失败')
    closeCreditHistoryDialog()
  } finally {
    loadingCreditHistory.value = false
  }
}

const closeCreditHistoryDialog = () => {
  selectedCreditHistoryUser.value = null
  creditHistoryRecords.value = []
  loadingCreditHistory.value = false
}

const handleAdjustCredit = async (userId) => {
  const draft = creditDrafts.value[userId]
  if (!draft) return
  try {
    clearMessage()
    await adjustCredit({ userId, credit: Number(draft.credit), reason: draft.reason })
    creditDrafts.value = { ...creditDrafts.value, [userId]: { credit: 0, reason: '' } }
    await loadUsers()
    closeCreditDialog()
    showMessage('success', '信用分已调整')
  } catch (error) {
    showMessage('error', error.response?.data?.message || '信用分调整失败')
  }
}

onMounted(async () => {
  try {
    await loadUsers()
  } catch (error) {
    showMessage('error', error.response?.data?.message || error.message || '用户数据加载失败')
  } finally {
    loadingUsers.value = false
  }
})
</script>
