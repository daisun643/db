<template>
  <div class="page-container system-admin-page">
    <AdminPageHeader title="交易纠纷" eyebrow="交易治理" description="处理交易纠纷并记录仲裁结果。" />
    <div class="card">
      <h2>交易纠纷仲裁</h2>
      <div v-if="message" :class="[`${message.type}-message`, 'section-message']">{{ message.text }}</div>
      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="disputes.length === 0" class="muted">暂无纠纷工单</div>
      <div v-else class="report-list">
        <article v-for="dispute in disputes" :key="dispute.ticketID" class="report-item dispute-item">
          <div>
            <strong>订单 #{{ dispute.transactionID }} · 工单 #{{ dispute.ticketID }}</strong>
            <div class="dispute-context">
              <span>{{ dispute.productTitle || '未知商品' }}</span>
              <span>金额 ¥{{ Number(dispute.transactionAmount || 0).toFixed(2) }}</span>
              <span>买家 {{ dispute.buyerName || '-' }}</span>
              <span>卖家 {{ dispute.sellerName || '-' }}</span>
            </div>
            <p>{{ dispute.reason }}</p>
            <small>
              {{ dispute.username || '申请人' }} · {{ dispute.status }}
              <template v-if="dispute.arbitratorName"> · 仲裁员 {{ dispute.arbitratorName }}</template>
            </small>
            <div v-if="dispute.decision" class="dispute-result">
              <strong>仲裁结果</strong>
              <p>{{ dispute.decision }}</p>
              <small>
                退款 ¥{{ Number(dispute.refundAmount || 0).toFixed(2) }}
                <template v-if="dispute.resolvedTime"> · {{ formatDate(dispute.resolvedTime) }}</template>
              </small>
            </div>
          </div>
          <form class="dispute-form" @submit.prevent="handleResolveDispute(dispute)">
            <input
              v-model="disputeDrafts[dispute.ticketID].decision"
              type="text"
              placeholder="仲裁结论"
              :disabled="dispute.status !== 'Open'"
              required
            />
            <input
              v-model.number="disputeDrafts[dispute.ticketID].refundAmount"
              type="number"
              min="0"
              :max="dispute.transactionAmount || undefined"
              step="0.01"
              placeholder="退款金额"
              :disabled="dispute.status !== 'Open'"
              required
            />
            <button class="btn" type="submit" :disabled="dispute.status !== 'Open'">结案</button>
          </form>
        </article>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import AdminPageHeader from '../../components/system/AdminPageHeader.vue'
import { getDisputes, resolveDispute } from '../../api'
import { useTimedMessage } from '../../composables/useTimedMessage'
import './systemAdmin.css'

const disputes = ref([])
const disputeDrafts = ref({})
const loading = ref(true)
const { message, clearMessage, showMessage } = useTimedMessage()

const formatDate = (value) => value ? new Date(value).toLocaleString('zh-CN', {
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
}) : ''

const loadDisputes = async () => {
  const res = await getDisputes()
  disputes.value = res.data
  disputeDrafts.value = Object.fromEntries(disputes.value.map(dispute => [
    dispute.ticketID,
    disputeDrafts.value[dispute.ticketID] || { decision: '', refundAmount: 0 },
  ]))
}

const handleResolveDispute = async (dispute) => {
  const draft = disputeDrafts.value[dispute.ticketID]
  if (!draft) return
  try {
    clearMessage()
    await resolveDispute(dispute.ticketID, {
      decision: draft.decision,
      refundAmount: draft.refundAmount || 0,
    })
    await loadDisputes()
    showMessage('success', '纠纷已结案')
  } catch (error) {
    showMessage('error', error.response?.data?.message || '纠纷处理失败')
  }
}

onMounted(async () => {
  try {
    await loadDisputes()
  } catch (error) {
    showMessage('error', error.response?.data?.message || error.message || '纠纷数据加载失败')
  } finally {
    loading.value = false
  }
})
</script>
