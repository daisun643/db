<template>
  <div class="page-container">
    <div class="page-header">
      <h1 class="page-title">资金流水</h1>
    </div>

    <!-- 摘要卡片 -->
    <section class="summary-cards">
      <div class="card">
        <div class="card-label">总资产</div>
        <!-- 改为 totalBalance -->
        <div class="card-value">¥ {{ summary.totalBalance?.toFixed(2) || '0.00' }}</div>
      </div>
      <div class="card">
        <div class="card-label">冻结金额</div>
        <!-- 保持 frozenAmount -->
        <div class="card-value">¥ {{ summary.frozenAmount?.toFixed(2) || '0.00' }}</div>
      </div>
      <div class="card">
        <div class="card-label">可用余额</div>
        <!-- 改为 availableBalance -->
        <div class="card-value">¥ {{ summary.availableBalance?.toFixed(2) || '0.00' }}</div>
      </div>
      <div class="card">
        <div class="card-label">净收益</div>
        <!-- 保持 netAmount -->
        <div class="card-value">¥ {{ summary.netAmount?.toFixed(2) || '0.00' }}</div>
      </div>
    </section>

    <!-- 流水列表 -->
    <section class="flow-section">
      <div class="flow-stats">
        <span>共 {{ totalCount }} 条记录</span>
        <span>总收入：¥ {{ summary.totalIncome?.toFixed(2) || '0.00' }}</span>
        <span>总支出：¥ {{ summary.totalExpense?.toFixed(2) || '0.00' }}</span>
      </div>

      <div v-if="loading" class="loading">加载中...</div>
      <table v-else class="flow-table">
        <thead>
          <tr>
            <th>时间</th>
            <th>类型</th>
            <th>金额</th>
            <th>状态</th>
            <th>描述</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="flow in flows" :key="flow.transactionId || flow.time">
            <td>{{ formatDate(flow.time) }}</td>
            <td>
              <span :class="['badge', flow.type === '收入' ? 'badge-green' : 'badge-red']">
                {{ flow.type }}
              </span>
            </td>
            <td :class="flow.type === '收入' ? 'income' : 'expense'">
              {{ flow.type === '收入' ? '+' : '-' }} ¥{{ flow.amount?.toFixed(2) }}
            </td>
            <td>{{ flow.status || '—' }}</td>
            <td>{{ flow.description || '—' }}</td>
          </tr>
          <tr v-if="flows.length === 0">
            <td colspan="5" class="empty-state">暂无流水记录</td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</template>

<script>
import { ref, onMounted } from 'vue'
import { getFinanceFlows, getFinanceSummary } from '../api/index.js'

export default {
  name: 'FinanceView',
  setup() {
    const flows = ref([])
    const summary = ref({})
    const totalCount = ref(0)
    const loading = ref(false)

    const formatDate = (isoString) => {
      if (!isoString) return '—'
      const d = new Date(isoString)
      return d.toLocaleString('zh-CN', { hour12: false })
    }

    const fetchData = async () => {
      loading.value = true
      try {
        const [flowsRes, summaryRes] = await Promise.all([
          getFinanceFlows(),
          getFinanceSummary()
        ])

        console.log('summaryRes 完整响应:', summaryRes)
        console.log('summaryRes.data 内容:', summaryRes.data)

        flows.value = flowsRes.data.flows || []
        totalCount.value = flowsRes.data.totalCount || 0
        summary.value = summaryRes.data || {}
          console.log('summary.value 赋值后:', summary.value)
        } catch (err) {
          console.error('获取资金流水失败', err)
        } finally {
          loading.value = false
        }
    }

    onMounted(() => {
      fetchData()
    })

    return {
      flows,
      summary,
      totalCount,
      loading,
      formatDate
    }
  }
}
</script>

<style scoped>
.page-container {
  padding: 20px;
}
.page-header {
  margin-bottom: 24px;
}
.page-title {
  font-size: 24px;
  font-weight: 600;
  color: #1e293b;
}

.summary-cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 16px;
  margin-bottom: 32px;
}
.card {
  background: #fff;
  border-radius: 8px;
  padding: 16px 20px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
  border: 1px solid #e9edf4;
}
.card-label {
  font-size: 14px;
  color: #64748b;
  margin-bottom: 6px;
}
.card-value {
  font-size: 24px;
  font-weight: 600;
  color: #0f172a;
}

.flow-section {
  background: #fff;
  border-radius: 8px;
  padding: 20px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
}
.flow-stats {
  display: flex;
  gap: 24px;
  font-size: 14px;
  color: #475569;
  margin-bottom: 16px;
}
.flow-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}
.flow-table th {
  text-align: left;
  padding: 10px 8px;
  border-bottom: 2px solid #e2e8f0;
  color: #475569;
  font-weight: 500;
}
.flow-table td {
  padding: 10px 8px;
  border-bottom: 1px solid #f1f5f9;
}
.flow-table tr:hover td {
  background-color: #f8fafc;
}
.badge {
  display: inline-block;
  padding: 2px 10px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 500;
}
.badge-green {
  background: #dcfce7;
  color: #166534;
}
.badge-red {
  background: #fee2e2;
  color: #991b1b;
}
.income {
  color: #16a34a;
}
.expense {
  color: #dc2626;
}
.empty-state {
  text-align: center;
  color: #94a3b8;
  padding: 32px 0;
}
.loading {
  text-align: center;
  padding: 24px;
  color: #94a3b8;
}
</style>