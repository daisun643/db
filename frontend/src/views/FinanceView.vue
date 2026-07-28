<template>
  <div class="page-container finance-page">
    <section class="finance-hero">
      <div>
        <span class="finance-eyebrow"><i></i>FINANCE CENTER</span>
        <h1>每一笔流动，<span>都清晰可见。</span></h1>
        <p>集中查看账户余额、冻结资金与交易收支，让校园交易的每一步都有迹可循。</p>
      </div>
      <div class="finance-balance">
        <span>AVAILABLE BALANCE</span>
        <strong>¥ {{ summary.availableAmount?.toFixed(2) || '0.00' }}</strong>
        <small>当前可用余额</small>
        <div><span>总收入 <b>¥{{ summary.totalIncome?.toFixed(2) || '0.00' }}</b></span><span>总支出 <b>¥{{ summary.totalExpense?.toFixed(2) || '0.00' }}</b></span></div>
      </div>
    </section>

    <!-- 摘要卡片 -->
    <section class="summary-cards">
      <div class="card">
        <div class="card-label">总资产</div>
        <div class="card-value">¥ {{ summary.balance?.toFixed(2) || '0.00' }}</div>
      </div>
      <div class="card">
        <div class="card-label">冻结金额</div>
        <div class="card-value">¥ {{ summary.frozenAmount?.toFixed(2) || '0.00' }}</div>
      </div>
      <div class="card">
        <div class="card-label">可用余额</div>
        <div class="card-value">¥ {{ summary.availableAmount?.toFixed(2) || '0.00' }}</div>
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
      <div v-else class="flow-table-wrap"><table class="flow-table">
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
      </table></div>
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

<style scoped>
.finance-page { width: 100%; max-width: 1480px; margin: 0 auto; color: #11182b; }
.finance-hero { position: relative; isolation: isolate; min-height: 315px; display: grid; grid-template-columns: minmax(0,1.15fr) minmax(340px,.85fr); align-items: center; gap: clamp(2rem,5vw,5rem); overflow: hidden; padding: clamp(2rem,4.5vw,4rem); border-radius: 30px; color: #fff; background: radial-gradient(circle at 12% 0%,rgba(62,218,220,.24),transparent 30%),linear-gradient(135deg,#13263d 0%,#263a68 50%,#604cda 100%); box-shadow: 0 28px 65px rgba(31,44,91,.2); }
.finance-hero::before { content:''; position:absolute; inset:0; z-index:-1; opacity:.14; background-image:linear-gradient(rgba(255,255,255,.17) 1px,transparent 1px),linear-gradient(90deg,rgba(255,255,255,.17) 1px,transparent 1px); background-size:42px 42px; mask-image:linear-gradient(to right,#000,transparent 75%); }
.finance-eyebrow { display:inline-flex; align-items:center; gap:.65rem; color:#9eeceb; font-size:.68rem; font-weight:800; letter-spacing:.18em; }
.finance-eyebrow i { width:8px; height:8px; border-radius:50%; background:#65f2c9; box-shadow:0 0 0 6px rgba(101,242,201,.11),0 0 18px rgba(101,242,201,.72); }
.finance-hero h1 { max-width:700px; margin-top:1.2rem; font-size:clamp(2.5rem,4.7vw,4.6rem); line-height:1; letter-spacing:-.055em; }
.finance-hero h1 span { color:#a7eeeb; }
.finance-hero p { max-width:580px; margin-top:1.2rem; color:rgba(255,255,255,.67); line-height:1.8; }
.finance-balance { padding:1.4rem; border:1px solid rgba(255,255,255,.17); border-radius:24px; background:rgba(8,16,38,.36); box-shadow:0 22px 50px rgba(5,13,33,.25); backdrop-filter:blur(22px); }
.finance-balance > span { color:#99e3df; font-size:.62rem; font-weight:800; letter-spacing:.14em; }
.finance-balance > strong { display:block; margin-top:.7rem; font-size:clamp(2.2rem,4vw,3.5rem); letter-spacing:-.05em; }
.finance-balance > small { color:rgba(255,255,255,.44); }
.finance-balance > div { display:grid; grid-template-columns:1fr 1fr; gap:.55rem; margin-top:1.2rem; }
.finance-balance > div span { display:flex; flex-direction:column; padding:.8rem; border-radius:13px; color:rgba(255,255,255,.45); background:rgba(255,255,255,.07); font-size:.68rem; }
.finance-balance > div b { margin-top:.15rem; color:#fff; font-size:.9rem; }
.finance-page .summary-cards { grid-template-columns:repeat(4,minmax(0,1fr)); gap:1rem; margin:1.25rem 0; }
.finance-page .card { position:relative; overflow:hidden; padding:1.3rem; border:1px solid rgba(25,34,59,.08); border-radius:20px; box-shadow:0 12px 32px rgba(29,35,58,.05); }
.finance-page .card::after { content:''; position:absolute; width:70px; height:70px; right:-30px; bottom:-36px; border-radius:50%; background:#dcd7ff; opacity:.55; }
.finance-page .card-label { color:#858b9d; font-size:.72rem; font-weight:700; letter-spacing:.04em; }
.finance-page .card-value { margin-top:.45rem; color:#171d31; font-size:1.45rem; letter-spacing:-.035em; }
.finance-page .flow-section { padding:1.3rem; border:1px solid rgba(25,34,59,.08); border-radius:22px; box-shadow:0 12px 35px rgba(29,35,58,.05); }
.finance-page .flow-stats { flex-wrap:wrap; gap:.5rem; margin-bottom:1rem; }
.finance-page .flow-stats span { padding:.42rem .7rem; border-radius:999px; background:#f2f0ff; color:#6254c8; font-size:.72rem; }
.flow-table-wrap { overflow-x:auto; border:1px solid #ececf2; border-radius:15px; }
.finance-page .flow-table { min-width:720px; }
.finance-page .flow-table th { padding:.8rem; border-bottom:1px solid #e8e9ef; background:#f8f8fc; color:#888e9e; font-size:.68rem; font-weight:750; letter-spacing:.08em; }
.finance-page .flow-table td { padding:.85rem .8rem; }
.finance-page .flow-table tr:last-child td { border-bottom:0; }
.finance-page .flow-table tr:hover td { background:#faf9ff; }
@media(max-width:900px){.finance-hero{grid-template-columns:1fr;padding:2rem;border-radius:24px}.finance-balance{max-width:620px}.finance-page .summary-cards{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:640px){.finance-hero{min-height:auto;gap:1.5rem;padding:1.45rem;border-radius:20px}.finance-hero h1{font-size:2.05rem}.finance-hero p{font-size:.86rem}.finance-balance{padding:1rem;border-radius:18px}.finance-page .summary-cards{gap:.65rem}.finance-page .card{padding:1rem;border-radius:16px}.finance-page .card-value{font-size:1.05rem}.finance-page .flow-section{padding:.8rem;border-radius:18px}}
</style>
