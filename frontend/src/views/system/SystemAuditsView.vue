<template>
  <div class="page-container system-admin-page">
    <AdminPageHeader title="内容审核" eyebrow="内容治理" description="审核命中敏感词的帖子和评论。" />
    <div class="card">
      <h2>内容审核队列</h2>
      <div v-if="message" :class="[`${message.type}-message`, 'section-message']">{{ message.text }}</div>
      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="postAudits.length === 0" class="muted">暂无待审核内容</div>
      <div v-else class="report-list">
        <article v-for="audit in postAudits" :key="audit.auditID" class="report-item">
          <div>
            <strong>{{ auditTitle(audit) }}</strong>
            <p>{{ auditContent(audit) }}</p>
            <small>{{ audit.targetType || 'Post' }} #{{ audit.targetID }} · 命中词：{{ audit.triggerWord || '-' }} · {{ audit.status }}</small>
          </div>
          <div class="report-actions">
            <button class="btn" @click="handlePostAudit(audit, 'approve')">通过</button>
            <button class="btn" @click="handlePostAudit(audit, 'reject')">拒绝</button>
          </div>
        </article>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AdminPageHeader from '../../components/system/AdminPageHeader.vue'
import { approvePostAudit, getPostAudits, rejectPostAudit } from '../../api'
import { useTimedMessage } from '../../composables/useTimedMessage'
import './systemAdmin.css'

const postAudits = ref([])
const loading = ref(true)
const { message, clearMessage, showMessage } = useTimedMessage()

const auditTitle = (audit) => (audit.targetType || '').toLowerCase() === 'comment'
  ? `评论 #${audit.targetID}`
  : audit.post?.title || `帖子 #${audit.targetID}`

const auditContent = (audit) => (audit.targetType || '').toLowerCase() === 'comment'
  ? audit.comment?.content || '暂无评论内容'
  : audit.post?.contentPreview || '暂无帖子内容'

const loadPostAudits = async () => {
  const res = await getPostAudits()
  postAudits.value = res.data
}

const handlePostAudit = async (audit, action) => {
  try {
    clearMessage()
    if (action === 'approve') await approvePostAudit(audit.auditID)
    else await rejectPostAudit(audit.auditID)
    await loadPostAudits()
    showMessage('success', '内容审核已处理')
  } catch (error) {
    showMessage('error', error.response?.data?.message || '内容审核失败')
  }
}

onMounted(async () => {
  try {
    await loadPostAudits()
  } catch (error) {
    showMessage('error', error.response?.data?.message || error.message || '审核队列加载失败')
  } finally {
    loading.value = false
  }
})
</script>
