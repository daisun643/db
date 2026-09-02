<template>
  <div class="page-container system-admin-page">
    <AdminPageHeader title="帖子管理" eyebrow="内容治理" description="管理帖子状态与可见性。" />
    <div class="card">
      <h2>帖子状态管理</h2>
      <div v-if="message" :class="[`${message.type}-message`, 'section-message']">{{ message.text }}</div>
      <div class="toolbar-row">
        <select v-model="postStatusFilter" @change="loadManagedPosts">
          <option value="">可见帖子</option>
          <option value="Active">正常</option>
          <option value="Pinned">置顶</option>
          <option value="Elite">精华</option>
          <option value="PendingReview">待审核</option>
          <option value="Banned">已封禁</option>
          <option value="Deleted">已删除</option>
        </select>
        <button class="btn" @click="loadManagedPosts">刷新</button>
      </div>
      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="managedPosts.length === 0" class="muted">暂无帖子</div>
      <div v-else class="report-list">
        <article v-for="post in managedPosts" :key="post.postID" class="report-item">
          <div>
            <strong>{{ post.title }}</strong>
            <p>{{ post.contentPreview || '暂无内容' }}</p>
            <small>{{ post.forumName || '未分区' }} · {{ post.username || '匿名用户' }} · {{ post.status }}</small>
          </div>
          <div class="report-actions post-status-actions">
            <button class="btn" @click="handlePostStatus(post, 'pin')">置顶</button>
            <button class="btn" @click="handlePostStatus(post, 'elite')">精华</button>
            <button class="btn" @click="handlePostStatus(post, 'ban')">封禁</button>
            <button class="btn" @click="handlePostStatus(post, 'restore')">恢复</button>
            <button class="btn" @click="handlePostStatus(post, 'delete')">删除</button>
          </div>
        </article>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AdminPageHeader from '../../components/system/AdminPageHeader.vue'
import { changePostStatus, getPosts } from '../../api'
import { useTimedMessage } from '../../composables/useTimedMessage'
import './systemAdmin.css'

const managedPosts = ref([])
const postStatusFilter = ref('')
const loading = ref(true)
const { message, clearMessage, showMessage } = useTimedMessage()

const loadManagedPosts = async () => {
  loading.value = true
  try {
    const res = await getPosts({ status: postStatusFilter.value || undefined, pageSize: 50 })
    managedPosts.value = res.data
  } catch (error) {
    showMessage('error', error.response?.data?.message || error.message || '帖子加载失败')
  } finally {
    loading.value = false
  }
}

const handlePostStatus = async (post, action) => {
  try {
    clearMessage()
    await changePostStatus(post.postID, { action })
    await loadManagedPosts()
    showMessage('success', '帖子状态已更新')
  } catch (error) {
    showMessage('error', error.response?.data?.message || '帖子状态更新失败')
  }
}

onMounted(loadManagedPosts)
</script>
