<template>
  <div class="page-container system-admin-page">
    <AdminPageHeader title="举报工单" eyebrow="内容治理" description="审核用户举报并记录处理结果。" />
    <div class="card">
      <h2>举报工单</h2>
      <div v-if="message" :class="[`${message.type}-message`, 'section-message']">{{ message.text }}</div>
      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="reports.length === 0" class="muted">暂无举报</div>
      <div v-else class="report-list">
        <article v-for="report in reports" :key="report.reportID" class="report-item">
          <div>
            <strong>{{ report.targetType }} #{{ report.targetID }}</strong>
            <p>{{ report.reason }}</p>
            <small>{{ report.reporterName || '匿名用户' }} · {{ report.status }}</small>
          </div>
          <div class="report-actions">
            <button class="btn" :disabled="report.status !== 'Pending'" @click="handleReviewReport(report, 'approve')">通过</button>
            <button class="btn" :disabled="report.status !== 'Pending'" @click="handleReviewReport(report, 'reject')">驳回</button>
          </div>
        </article>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AdminPageHeader from '../../components/system/AdminPageHeader.vue'
import { getReports, reviewReport } from '../../api'
import { useTimedMessage } from '../../composables/useTimedMessage'
import './systemAdmin.css'

const reports = ref([])
const loading = ref(true)
const { message, clearMessage, showMessage } = useTimedMessage()

const loadReports = async () => {
  const res = await getReports()
  reports.value = res.data
}

const handleReviewReport = async (report, action) => {
  try {
    clearMessage()
    await reviewReport(report.reportID, {
      action,
      result: action === 'approve' ? '举报成立，已处理目标内容' : '举报不成立',
    })
    await loadReports()
    showMessage('success', '举报已处理')
  } catch (error) {
    showMessage('error', error.response?.data?.message || '举报处理失败')
  }
}

onMounted(async () => {
  try {
    await loadReports()
  } catch (error) {
    showMessage('error', error.response?.data?.message || error.message || '举报数据加载失败')
  } finally {
    loading.value = false
  }
})
</script>
