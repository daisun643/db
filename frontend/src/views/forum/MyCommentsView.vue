<template>
  <div class="tab-content">
    <div v-if="loadingComments" class="loading">加载中...</div>
    <div v-else class="comment-list">
      <article v-for="comment in myComments" :key="comment.commentID" class="comment-card">
        <p class="comment-content">{{ comment.content }}</p>
        <div class="comment-meta">
          <span class="comment-target">
            评论于
            <router-link :to="`/forums/post/${comment.postID}`" class="comment-link">
              《{{ comment.postTitle || '帖子' }}》
            </router-link>
            <template v-if="comment.forumName">
              ·
              <router-link :to="`/forums/board/${comment.forumID}`" class="comment-link">
                {{ comment.forumName }}
              </router-link>
            </template>
          </span>
          <span class="comment-time">{{ formatDate(comment.createTime) }}</span>
        </div>
        <div class="comment-footer">
          <span v-if="comment.status !== 'Active'" class="comment-status" :class="`status-${statusClass(comment.status)}`">
            {{ statusText(comment.status) }}
          </span>
          <button class="comment-delete" @click="handleDelete(comment)">删除</button>
        </div>
      </article>
      <div v-if="myComments.length === 0" class="empty-state">
        <p>暂无评论</p>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { getMyComments, deleteComment } from '../../api'
import { useForum } from '../../composables/useForum'

const { error } = useForum()

const myComments = ref([])
const loadingComments = ref(false)

const loadComments = async () => {
  try {
    loadingComments.value = true
    const res = await getMyComments()
    myComments.value = res.data
  } catch (e) {
    error.value = '无法加载我的评论: ' + (e.response?.data?.message || e.message)
  } finally {
    loadingComments.value = false
  }
}

const handleDelete = async (comment) => {
  if (!window.confirm('确定删除这条评论吗？')) return
  try {
    await deleteComment(comment.commentID)
    myComments.value = myComments.value.filter(item => item.commentID !== comment.commentID)
  } catch (e) {
    error.value = '删除评论失败: ' + (e.response?.data?.message || e.message)
  }
}

const statusText = (status) => ({
  PendingReview: '审核中',
  Banned: '已屏蔽',
}[status] || status || '未知')

const statusClass = (status) => ({
  PendingReview: 'pending',
  Banned: 'banned',
}[status] || 'unknown')

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

onMounted(loadComments)
</script>

<style scoped>
.tab-content {
  min-height: 400px;
  min-width: 0;
}

.comment-list {
  margin-top: .9rem;
  border: 1px solid #e4e7ec;
  border-radius: 6px;
  background: #fff;
  overflow: hidden;
}

.comment-card {
  padding: .95rem 1.1rem;
  border-bottom: 1px solid #eef1f5;
}

.comment-card:last-child {
  border-bottom: none;
}

.comment-content {
  margin: 0;
  color: var(--forum-ink, #11172a);
  font-size: .92rem;
  line-height: 1.55;
  white-space: pre-wrap;
  word-break: break-word;
}

.comment-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: .35rem .6rem;
  margin-top: .5rem;
  color: #778398;
  font-size: .78rem;
}

.comment-link {
  color: #266bc4;
  text-decoration: none;
}

.comment-link:hover {
  text-decoration: underline;
}

.comment-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: .45rem;
}

.comment-status {
  padding: .12rem .55rem;
  border-radius: 999px;
  font-size: .72rem;
}

.status-pending {
  color: #b7791f;
  background: #fdf3d8;
}

.status-banned {
  color: #b42318;
  background: #fdecea;
}

.status-unknown {
  color: #718096;
  background: #eef1f5;
}

.comment-delete {
  border: none;
  background: transparent;
  color: #b42318;
  font-size: .78rem;
  cursor: pointer;
}

.comment-delete:hover {
  text-decoration: underline;
}

.empty-state {
  padding: 3rem 1rem;
  color: var(--text-secondary);
  text-align: center;
}
</style>
