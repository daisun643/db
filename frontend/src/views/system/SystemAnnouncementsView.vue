<template>
  <div class="page-container system-admin-page">
    <AdminPageHeader title="系统公告" eyebrow="运营工具" description="向全体用户或指定用户发布系统通知。" />
    <div class="card">
      <h2>发布系统公告</h2>
      <div v-if="message" :class="[`${message.type}-message`, 'section-message']">{{ message.text }}</div>
      <form class="announcement-form" @submit.prevent="handleCreateAnnouncement">
        <input v-model="announcementForm.title" type="text" placeholder="系统公告标题" required />
        <textarea v-model="announcementForm.content" placeholder="系统公告内容" rows="3" required></textarea>
        <div class="announcement-form-actions">
          <label class="announcement-target-label">
            <span>推送范围</span>
            <select v-model="announcementForm.targetType">
              <option value="all">全体用户</option>
              <option value="specified">指定用户</option>
            </select>
          </label>
          <div v-if="announcementForm.targetType === 'specified'" class="announcement-user-select">
            <div v-if="announcementForm.receiverUserIDs.length" class="announcement-user-chips">
              <span v-for="uid in announcementForm.receiverUserIDs" :key="uid" class="manager-tag">
                {{ getUserDisplayName(uid) }}
                <button type="button" @click="removeReceiverUser(uid)">×</button>
              </span>
            </div>
            <div class="announcement-user-picker">
              <select v-model.number="announcementUserDraft" @change="addReceiverUser">
                <option disabled :value="0">选择用户添加</option>
                <option
                  v-for="user in users"
                  :key="user.userID"
                  :value="user.userID"
                  :disabled="announcementForm.receiverUserIDs.includes(user.userID)"
                >
                  {{ user.username }} · {{ user.email }}
                </option>
              </select>
            </div>
          </div>
          <button class="btn btn-primary" type="submit" :disabled="loadingAnnouncements">发布</button>
        </div>
      </form>
    </div>

    <div class="card">
      <h2>系统公告列表</h2>
      <div class="toolbar-row"><button class="btn" @click="loadAnnouncements">刷新</button></div>
      <div v-if="loadingAnnouncements" class="loading">加载中...</div>
      <div v-else-if="announcements.length === 0" class="muted">暂无系统公告</div>
      <div v-else class="report-list">
        <article v-for="announcement in announcements" :key="announcement.id" class="report-item">
          <div>
            <strong>{{ announcement.title }}</strong>
            <p>{{ announcement.content }}</p>
            <small>{{ formatDate(announcement.date) }}</small>
          </div>
          <div class="report-actions">
            <button class="btn" @click="handleDeleteAnnouncement(announcement.id)">删除</button>
          </div>
        </article>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AdminPageHeader from '../../components/system/AdminPageHeader.vue'
import { createSystemNotification, deleteNotification, getAnnouncements, getUsers } from '../../api'
import { useTimedMessage } from '../../composables/useTimedMessage'
import './systemAdmin.css'

const users = ref([])
const announcements = ref([])
const loadingAnnouncements = ref(false)
const announcementUserDraft = ref(0)
const announcementForm = ref({ title: '', content: '', targetType: 'all', receiverUserIDs: [] })
const { message, clearMessage, showMessage } = useTimedMessage()

const formatDate = (value) => value ? new Date(value).toLocaleString('zh-CN', {
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
}) : ''

const getUserDisplayName = (uid) => {
  const user = users.value.find(item => item.userID === uid)
  return user ? `${user.username} (${user.email})` : `用户 #${uid}`
}

const addReceiverUser = () => {
  const uid = announcementUserDraft.value
  if (uid && !announcementForm.value.receiverUserIDs.includes(uid)) {
    announcementForm.value.receiverUserIDs.push(uid)
  }
  announcementUserDraft.value = 0
}

const removeReceiverUser = (uid) => {
  announcementForm.value.receiverUserIDs = announcementForm.value.receiverUserIDs.filter(id => id !== uid)
}

const loadAnnouncements = async () => {
  loadingAnnouncements.value = true
  try {
    const res = await getAnnouncements({ page: 1, pageSize: 50 })
    announcements.value = res.data.items || []
  } catch (error) {
    announcements.value = []
    showMessage('error', error.response?.data?.message || error.message || '系统公告加载失败')
  } finally {
    loadingAnnouncements.value = false
  }
}

const loadUsers = async () => {
  const res = await getUsers()
  users.value = res.data
}

const handleCreateAnnouncement = async () => {
  if (!announcementForm.value.title.trim() || !announcementForm.value.content.trim()) return
  try {
    clearMessage()
    loadingAnnouncements.value = true
    const payload = {
      title: announcementForm.value.title.trim(),
      content: announcementForm.value.content.trim(),
    }
    if (announcementForm.value.targetType === 'specified') {
      payload.receiverUserIDs = announcementForm.value.receiverUserIDs
    }
    await createSystemNotification(payload)
    announcementForm.value = { title: '', content: '', targetType: 'all', receiverUserIDs: [] }
    announcementUserDraft.value = 0
    await loadAnnouncements()
    showMessage('success', '系统公告已发布')
  } catch (error) {
    showMessage('error', error.response?.data?.message || '系统公告发布失败')
  } finally {
    loadingAnnouncements.value = false
  }
}

const handleDeleteAnnouncement = async (id) => {
  try {
    clearMessage()
    await deleteNotification(id)
    await loadAnnouncements()
    showMessage('success', '系统公告已删除')
  } catch (error) {
    showMessage('error', error.response?.data?.message || '系统公告删除失败')
  }
}

onMounted(async () => {
  await Promise.allSettled([loadUsers(), loadAnnouncements()])
})
</script>
