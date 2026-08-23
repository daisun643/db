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
            <p v-if="report.description" class="muted report-desc">补充说明：{{ report.description }}</p>
            <small>{{ report.reporterName || '匿名用户' }} · {{ report.status }}</small>
          </div>
          <div class="report-actions">
            <button class="btn" type="button" @click="openReportDetail(report)">查看详情</button>
            <button class="btn" :disabled="report.status !== 'Pending'" @click="handleReviewReport(report, 'approve')">通过</button>
            <button class="btn" :disabled="report.status !== 'Pending'" @click="handleReviewReport(report, 'reject')">驳回</button>
          </div>
        </article>
      </div>
    </div>

    <div v-if="reportDetail" class="detail-backdrop" @click.self="reportDetail = null">
      <div class="report-detail-dialog">
        <div class="dialog-header">
          <div>
            <h3>举报详情 #{{ reportDetail.reportID }}</h3>
            <p class="muted">{{ reportDetail.targetType }} #{{ reportDetail.targetID }} · {{ reportDetail.status }}</p>
          </div>
          <button class="icon-button" type="button" aria-label="关闭" @click="reportDetail = null">×</button>
        </div>
        <dl class="report-detail-body">
          <dt>举报人</dt>
          <dd>{{ reportDetail.reporterName || '匿名用户' }}</dd>
          <dt>举报原因</dt>
          <dd>{{ reportDetail.reason }}</dd>
          <template v-if="reportDetail.description">
            <dt>补充说明</dt>
            <dd>{{ reportDetail.description }}</dd>
          </template>
          <dt>被举报内容</dt>
          <dd v-if="reportDetail.target">
            <template v-if="reportDetail.target.title">标题：{{ reportDetail.target.title }}<br /></template>
            <template v-if="reportDetail.target.content">内容：{{ reportDetail.target.content }}<br /></template>
            状态：{{ reportDetail.target.status }}<br />
            发布者：{{ reportDetail.target.ownerName }}（ID {{ reportDetail.target.ownerID }}，信用分 {{ reportDetail.target.ownerCredit }}）
          </dd>
          <dd v-else class="muted">目标内容已不存在</dd>
          <template v-if="reportDetail.result">
            <dt>处理结果</dt>
            <dd>{{ reportDetail.result }}</dd>
          </template>
        </dl>
        <div class="dialog-footer">
          <button class="btn" type="button" @click="reportDetail = null">关闭</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AdminPageHeader from '../../components/system/AdminPageHeader.vue'
import { getReport, getReports, reviewReport } from '../../api'
import { useTimedMessage } from '../../composables/useTimedMessage'
import './systemAdmin.css'

const reports = ref([])
const reportDetail = ref(null)
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

const openReportDetail = async (report) => {
  try {
    clearMessage()
    const res = await getReport(report.reportID)
    reportDetail.value = res.data
  } catch (error) {
    showMessage('error', error.response?.data?.message || '加载举报详情失败')
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

<style scoped>
.report-desc {
  margin-top: 0.25rem;
}

.detail-backdrop {
  align-items: flex-start;
  background: rgba(15, 23, 42, 0.35);
  bottom: 0;
  display: flex;
  justify-content: center;
  left: 0;
  overflow-y: auto;
  padding: 3rem 1rem;
  position: fixed;
  right: 0;
  top: 0;
  z-index: 1200;
}

.report-detail-dialog {
  background: var(--surface, #fff);
  border: 1px solid var(--border, #e2e8f0);
  border-radius: var(--radius, 12px);
  box-shadow: 0 20px 45px rgba(15, 23, 42, 0.2);
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 560px;
  padding: 1.25rem;
  width: 100%;
}

.dialog-header {
  align-items: flex-start;
  display: flex;
  gap: 1rem;
  justify-content: space-between;
}

.dialog-header h3 {
  margin: 0;
}

.report-detail-body {
  display: grid;
  gap: 0.5rem 1rem;
  grid-template-columns: 6rem 1fr;
  margin: 0;
}

.report-detail-body dt {
  color: var(--muted, #64748b);
  font-weight: 600;
}

.report-detail-body dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
}
</style>
