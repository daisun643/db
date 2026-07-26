<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <h1 class="page-title">纠纷仲裁</h1>
        <p class="muted">处理交易纠纷、补充材料、退款拆分与信用分扣减。</p>
      </div>
      <button class="btn" @click="loadDisputes" :disabled="loading">
        {{ loading ? '刷新中...' : '刷新' }}
      </button>
    </div>

    <div v-if="error" class="error-message">{{ error }}</div>

    <div v-if="loading" class="loading">加载中...</div>
    <div v-else class="dispute-list">
      <article v-for="item in disputes" :key="item.ticketID" class="dispute-card">
        <div class="dispute-head">
          <div>
            <h2>#{{ item.ticketID }} {{ item.productTitle || '交易纠纷' }}</h2>
            <p>{{ item.reason }}</p>
          </div>
          <span :class="['badge', badgeClass(item.status)]">{{ statusText(item.status) }}</span>
        </div>

        <div class="dispute-meta">
          <span>订单 #{{ item.transactionID }}</span>
          <strong>冻结金额 ¥{{ money(item.transactionAmount) }}</strong>
          <span>订单状态：{{ orderStatusText(item.orderStatus) }}</span>
          <span>买家：{{ item.buyerName || '-' }}</span>
          <span>卖家：{{ item.sellerName || '-' }}</span>
          <span>仲裁员：{{ item.arbitratorName || '未分配' }}</span>
          <span>创建：{{ formatDate(item.createTime) }}</span>
        </div>

        <div v-if="item.decision" class="result-panel">
          <strong>裁决结果</strong>
          <p>{{ item.decision }}</p>
          <div class="dispute-meta">
            <span>买家退款 ¥{{ money(item.refundAmount || 0) }}</span>
            <span>卖家到账 ¥{{ money(item.sellerSettlementAmount || 0) }}</span>
            <span>完成时间：{{ formatDate(item.resolvedTime) }}</span>
          </div>
        </div>

        <form v-if="canResolve(item)" class="resolve-form" @submit.prevent="handleResolve(item)">
          <textarea v-model="resolveForms[item.ticketID].decision" placeholder="输入仲裁意见，例如：部分质量问题，买卖双方各承担部分责任" required></textarea>
          <div class="form-row">
            <select v-model="resolveForms[item.ticketID].responsibilityParty" required title="责任方决定信用分扣减">
              <option value="Buyer">买家责任</option>
              <option value="Seller">卖家责任</option>
              <option value="Both">双方责任</option>
              <option value="None">无责任</option>
            </select>
            <select v-model="resolveForms[item.ticketID].preset" @change="applyPreset(item)">
              <option value="custom">自定义退款</option>
              <option value="full">全额退款</option>
              <option value="partial">部分退款 50%</option>
              <option value="none">不退款</option>
            </select>
            <input v-model.number="resolveForms[item.ticketID].refundAmount" type="number" min="0" :max="item.transactionAmount" step="0.01" placeholder="买家退款金额" required />
            <span class="muted">卖家到账 ¥{{ money(sellerAmount(item)) }}；信用分按责任方扣减</span>
            <button class="btn btn-primary" type="submit" :disabled="resolvingId === item.ticketID">
              {{ resolvingId === item.ticketID ? '处理中...' : '提交裁决' }}
            </button>
          </div>
        </form>

        <form v-if="canResolve(item)" class="supplement-form" @submit.prevent="handleSupplement(item)">
          <input v-model="supplementForms[item.ticketID]" type="text" placeholder="要求双方补充材料的说明" required />
          <button class="btn" type="submit">要求补充材料</button>
        </form>
      </article>

      <div v-if="disputes.length === 0" class="empty-state">
        <p>暂无纠纷工单</p>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { getDisputes, requestDisputeSupplement, resolveDispute } from '../api'

const disputes = ref([])
const loading = ref(false)
const error = ref(null)
const resolvingId = ref(null)
const resolveForms = ref({})
const supplementForms = ref({})

const ensureForms = () => {
  const forms = { ...resolveForms.value }
  const supplements = { ...supplementForms.value }
  for (const dispute of disputes.value) {
    if (!forms[dispute.ticketID]) {
      forms[dispute.ticketID] = {
        decision: '',
        refundAmount: 0,
        responsibilityParty: 'Both',
        preset: 'custom',
      }
    }
    if (supplements[dispute.ticketID] === undefined) {
      supplements[dispute.ticketID] = ''
    }
  }
  resolveForms.value = forms
  supplementForms.value = supplements
}

const loadDisputes = async () => {
  try {
    loading.value = true
    error.value = null
    const res = await getDisputes()
    disputes.value = res.data
    ensureForms()
  } catch (e) {
    error.value = '无法加载纠纷工单: ' + (e.response?.data?.message || e.message)
  } finally {
    loading.value = false
  }
}

const canResolve = (item) => ['Open', 'NeedSupplement'].includes(item.status)

const applyPreset = (item) => {
  const form = resolveForms.value[item.ticketID]
  if (!form) return
  if (form.preset === 'full') {
    form.refundAmount = Number(item.transactionAmount || 0)
    form.responsibilityParty = 'Seller'
  }
  if (form.preset === 'partial') {
    form.refundAmount = Number(((item.transactionAmount || 0) / 2).toFixed(2))
    form.responsibilityParty = 'Both'
  }
  if (form.preset === 'none') {
    form.refundAmount = 0
    form.responsibilityParty = 'Buyer'
  }
}

const sellerAmount = (item) => {
  const form = resolveForms.value[item.ticketID]
  const refund = Number(form?.refundAmount || 0)
  return Math.max(0, Number(item.transactionAmount || 0) - refund)
}

const handleResolve = async (item) => {
  const form = resolveForms.value[item.ticketID]
  if (!form) return

  try {
    resolvingId.value = item.ticketID
    error.value = null
    await resolveDispute(item.ticketID, {
      decision: form.decision,
      refundAmount: Number(form.refundAmount || 0),
      responsibilityParty: form.responsibilityParty,
    })
    await loadDisputes()
  } catch (e) {
    error.value = '处理纠纷失败: ' + (e.response?.data?.message || e.message)
  } finally {
    resolvingId.value = null
  }
}

const handleSupplement = async (item) => {
  const message = supplementForms.value[item.ticketID]
  if (!message) return

  try {
    error.value = null
    await requestDisputeSupplement(item.ticketID, { message })
    supplementForms.value[item.ticketID] = ''
    await loadDisputes()
  } catch (e) {
    error.value = '补充材料通知失败: ' + (e.response?.data?.message || e.message)
  }
}

const money = (value) => Number(value || 0).toFixed(2)

const statusText = (status) => ({
  Open: '处理中',
  NeedSupplement: '待补充材料',
  Resolved: '已完成',
}[status] || status || '未知')

const orderStatusText = (status) => ({
  Pending: '待支付',
  Paid: '已支付',
  Disputed: '纠纷中',
  Completed: '已完成',
  Refunded: '已退款',
  Cancelled: '已取消',
}[status] || status || '未知')

const badgeClass = (status) => {
  if (status === 'Resolved') return 'badge-green'
  if (status === 'NeedSupplement') return 'badge-yellow'
  return 'badge-red'
}

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

onMounted(loadDisputes)
</script>

<style scoped>
.dispute-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.dispute-card,
.resolve-form,
.result-panel {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.dispute-card {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.dispute-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

.dispute-head h2 {
  font-size: 1.125rem;
}

.dispute-head p,
.result-panel p {
  color: var(--text-secondary);
  margin-top: 0.25rem;
}

.dispute-meta,
.form-row,
.supplement-form {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.dispute-meta {
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.resolve-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.resolve-form textarea,
.resolve-form input,
.resolve-form select,
.supplement-form input {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  padding: 0.625rem 0.75rem;
}

.resolve-form textarea {
  min-height: 88px;
  resize: vertical;
}

.supplement-form input {
  flex: 1;
  min-width: 260px;
}

@media (max-width: 760px) {
  .dispute-head,
  .form-row,
  .supplement-form {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>
