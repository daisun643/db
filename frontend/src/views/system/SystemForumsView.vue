<template>
  <div class="page-container system-admin-page">
    <AdminPageHeader title="论坛版块" eyebrow="社区配置" description="维护版块信息与版主管理关系。" />
    <div class="card">
      <h2>论坛版块管理</h2>
      <div v-if="message" :class="[`${message.type}-message`, 'section-message']">{{ message.text }}</div>
      <form class="inline-form" @submit.prevent="handleCreateForum">
        <input v-model="forumForm.forumName" type="text" placeholder="版块名称" required />
        <input v-model="forumForm.description" type="text" placeholder="版块描述" />
        <button class="btn btn-primary" type="submit">创建版块</button>
      </form>

      <div v-if="loading" class="loading">加载中...</div>
      <div v-else class="forum-admin-list">
        <article v-for="forum in forums" :key="forum.forumID" class="forum-admin-item">
          <div>
            <strong>{{ forum.forumName }}</strong>
            <p>{{ forum.description || '暂无描述' }}</p>
            <small>帖子 {{ forum.postCount || 0 }} · {{ forum.status }}</small>
            <div class="manager-tags">
              <span v-for="manager in forum.managers || []" :key="manager.userID" class="manager-tag">
                {{ manager.username || manager.email }}
                <button type="button" @click="handleRemoveManager(forum, manager)">×</button>
              </span>
              <span v-if="!forum.managers?.length" class="muted inline-muted">暂无版主</span>
            </div>
          </div>
          <form class="manager-form" @submit.prevent="handleAssignManager(forum)">
            <select v-model.number="forumManagerDrafts[forum.forumID]" required>
              <option disabled value="">选择版主</option>
              <option v-for="user in users" :key="user.userID" :value="user.userID">
                {{ user.username }} · {{ user.email }}
              </option>
            </select>
            <button class="btn" type="submit">指派</button>
          </form>
        </article>
        <div v-if="forums.length === 0" class="muted">暂无论坛版块</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AdminPageHeader from '../../components/system/AdminPageHeader.vue'
import { assignForumManager, createForum, getForums, getUsers, removeForumManager } from '../../api'
import { useTimedMessage } from '../../composables/useTimedMessage'
import './systemAdmin.css'

const forums = ref([])
const users = ref([])
const forumManagerDrafts = ref({})
const forumForm = ref({ forumName: '', description: '' })
const loading = ref(true)
const { message, clearMessage, showMessage } = useTimedMessage()

const loadForums = async () => {
  const res = await getForums()
  forums.value = res.data
}

const loadUsers = async () => {
  const res = await getUsers()
  users.value = res.data
}

const handleCreateForum = async () => {
  try {
    clearMessage()
    await createForum(forumForm.value)
    forumForm.value = { forumName: '', description: '' }
    await loadForums()
    showMessage('success', '论坛版块已创建')
  } catch (error) {
    showMessage('error', error.response?.data?.message || '论坛版块创建失败')
  }
}

const handleAssignManager = async (forum) => {
  const userId = forumManagerDrafts.value[forum.forumID]
  if (!userId) return

  try {
    clearMessage()
    await assignForumManager(forum.forumID, userId)
    forumManagerDrafts.value = { ...forumManagerDrafts.value, [forum.forumID]: '' }
    await loadForums()
    showMessage('success', '版主已指派')
  } catch (error) {
    showMessage('error', error.response?.data?.message || '版主指派失败')
  }
}

const handleRemoveManager = async (forum, manager) => {
  try {
    clearMessage()
    await removeForumManager(forum.forumID, manager.userID)
    await loadForums()
    showMessage('success', '版主已移除')
  } catch (error) {
    showMessage('error', error.response?.data?.message || '版主移除失败')
  }
}

onMounted(async () => {
  const results = await Promise.allSettled([loadForums(), loadUsers()])
  const failure = results.find(result => result.status === 'rejected')
  if (failure) {
    const error = failure.reason
    showMessage('error', error.response?.data?.message || error.message || '论坛数据加载失败')
  }
  loading.value = false
})
</script>
