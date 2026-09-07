<template>
  <div class="page-container system-dashboard">
    <div class="stat-cards">
      <router-link class="stat-card" to="/system-status/users">
        <div class="number">{{ stats.users }}</div>
        <div class="label">用户总数</div>
      </router-link>
      <router-link class="stat-card" to="/system-status/forums">
        <div class="number">{{ stats.forums }}</div>
        <div class="label">论坛数量</div>
      </router-link>
      <router-link class="stat-card" to="/system-status/posts">
        <div class="number">{{ stats.posts }}</div>
        <div class="label">帖子数量</div>
      </router-link>
      <router-link class="stat-card" to="/products">
        <div class="number">{{ stats.products }}</div>
        <div class="label">商品数量</div>
      </router-link>
    </div>

    <div v-if="errorMessage" class="error-message">{{ errorMessage }}</div>

    <section class="admin-launch-section">
      <div class="admin-launch-heading">
        <div><span>管理工具</span><h1>选择要管理的内容</h1></div>
        <p>各模块使用独立页面，可通过浏览器前进与后退切换。</p>
      </div>
      <div class="admin-launch-grid">
        <router-link
          v-for="card in adminCards"
          :key="card.section"
          :to="`/system-status/${card.section}`"
          class="card admin-launch-card"
        >
          <span class="launch-icon">{{ card.icon }}</span>
          <span class="launch-copy"><strong>{{ card.title }}</strong><small>{{ card.description }}</small></span>
          <b aria-hidden="true">→</b>
        </router-link>
      </div>
    </section>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import {
  getAnnouncements,
  getDisputes,
  getForums,
  getPostAudits,
  getPosts,
  getProducts,
  getReports,
  getUsers,
} from '../../api'

const stats = ref({ users: '-', forums: '-', posts: '-', products: '-' })
const counts = ref({ audits: '-', disputes: '-', reports: '-', announcements: '-' })
const errorMessage = ref('')

const adminCards = computed(() => [
  { section: 'forums', icon: '版', title: '论坛版块', description: `${stats.value.forums} 个版块` },
  { section: 'users', icon: '用', title: '用户管理', description: `${stats.value.users} 位用户` },
  { section: 'audits', icon: '审', title: '内容审核', description: `${counts.value.audits} 条待处理` },
  { section: 'posts', icon: '帖', title: '帖子管理', description: '状态与可见性' },
  { section: 'disputes', icon: '纠', title: '交易纠纷', description: `${counts.value.disputes} 个工单` },
  { section: 'reports', icon: '举', title: '举报工单', description: `${counts.value.reports} 条举报` },
  { section: 'announcements', icon: '公', title: '系统公告', description: `${counts.value.announcements} 条公告` },
])

const responseLength = (result) => result.status === 'fulfilled' && Array.isArray(result.value.data)
  ? result.value.data.length
  : 0

onMounted(async () => {
  const results = await Promise.allSettled([
    getUsers(),
    getForums(),
    getPosts(),
    getProducts(),
    getPostAudits(),
    getDisputes(),
    getReports(),
    getAnnouncements({ page: 1, pageSize: 1 }),
  ])
  const [users, forums, posts, products, audits, disputes, reports, announcements] = results

  stats.value = {
    users: responseLength(users),
    forums: responseLength(forums),
    posts: responseLength(posts),
    products: responseLength(products),
  }
  counts.value = {
    audits: responseLength(audits),
    disputes: responseLength(disputes),
    reports: responseLength(reports),
    announcements: announcements.status === 'fulfilled'
      ? (announcements.value.data.total ?? announcements.value.data.items?.length ?? 0)
      : 0,
  }

  if (results.every(result => result.status === 'rejected')) {
    const reason = results[0].reason
    errorMessage.value = '无法加载系统数据: ' + (reason?.response?.data?.message || reason?.message || '权限不足')
  }
})
</script>

<style scoped>
.system-dashboard { width:100%; max-width:1480px; margin:0 auto; color:#11172a; }
.stat-cards { grid-template-columns:repeat(4,minmax(0,1fr)); gap:.75rem; margin:1rem 0 0; }
.stat-card { position:relative; min-width:0; padding:1rem 1.1rem; border-radius:12px; color:inherit; background:#fff; box-shadow:none; text-align:left; text-decoration:none; transition:border-color .2s,box-shadow .2s,transform .2s; }
.stat-card:hover { border-color:#c8c4ee; box-shadow:0 5px 18px rgba(31,35,55,.055); transform:translateY(-1px); }
.stat-card .number { color:#342c87; font-size:1.55rem; letter-spacing:-.04em; }
.stat-card .label { margin-top:.15rem; color:#7d8595; font-size:.72rem; }
.error-message { margin-top:1rem; }
.admin-launch-section { margin-top:1.6rem; }
.admin-launch-heading { display:flex; align-items:flex-end; justify-content:space-between; gap:1rem; margin-bottom:.85rem; }
.admin-launch-heading span { display:block; margin-bottom:.2rem; color:var(--primary); font-size:.64rem; font-weight:750; letter-spacing:.1em; }
.admin-launch-heading h1 { color:#171d2e; font-size:1.15rem; letter-spacing:-.02em; }
.admin-launch-heading p { color:var(--text-secondary); font-size:.76rem; }
.admin-launch-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:.75rem; }
.admin-launch-card { min-width:0; display:flex; align-items:center; gap:.8rem; margin:0; padding:1rem; border-radius:12px; color:var(--text); background:#fff; box-shadow:none; text-align:left; text-decoration:none; transition:border-color .2s,box-shadow .2s,transform .2s; }
.admin-launch-card:hover { border-color:#c8c4ee; box-shadow:0 5px 18px rgba(31,35,55,.055); transform:translateY(-1px); }
.launch-copy { min-width:0; display:flex; flex:1; flex-direction:column; }
.admin-launch-card strong { font-size:.88rem; }
.admin-launch-card small { margin-top:.15rem; overflow:hidden; color:var(--text-secondary); font-size:.7rem; text-overflow:ellipsis; white-space:nowrap; }
.admin-launch-card b { color:#9aa1af; font-size:.9rem; }
.launch-icon { width:36px; height:36px; display:grid; place-items:center; flex:0 0 auto; border-radius:9px; color:#5145bf; background:#f0effc; font-size:.78rem; font-weight:750; }
.admin-launch-card:nth-child(3n+2) .launch-icon { color:#176c72; background:#eaf7f5; }
.admin-launch-card:nth-child(3n) .launch-icon { color:#315d9a; background:#edf3fb; }

@media(max-width:1000px){.stat-cards{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:900px){.admin-launch-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:600px){.admin-launch-heading{align-items:flex-start;flex-direction:column}.admin-launch-grid{grid-template-columns:1fr}}
</style>
