<template>
  <div class="home-page">
    <header class="welcome-header">
      <div>
        <p class="welcome-kicker">同济校园社区</p>
        <h1>你好，{{ displayName }}</h1>
        <p class="welcome-copy">从讨论、交易到消息，今天的校园动态都在这里。</p>
      </div>
      <router-link to="/forums" class="primary-action">
        发布新动态
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
          <path d="M5 12h14M13 6l6 6-6 6" />
        </svg>
      </router-link>
    </header>

    <section class="workspace" aria-labelledby="workspace-title">
      <div class="section-heading">
        <div>
          <h2 id="workspace-title">常用功能</h2>
          <p>快速进入你最常使用的校园服务</p>
        </div>
      </div>

      <div class="action-grid">
        <router-link to="/forums" class="action-card">
          <span class="action-icon purple">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
              <path d="M21 15a2 2 0 0 1-2 2H8l-5 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
            </svg>
          </span>
          <span class="action-copy"><strong>校园论坛</strong><small>浏览讨论或分享近况</small></span>
          <span class="action-arrow" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M5 12h14M13 6l6 6-6 6" />
            </svg>
          </span>
        </router-link>

        <router-link to="/products" class="action-card">
          <span class="action-icon teal">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
              <path d="M3 3h2l2.4 11.2a2 2 0 0 0 2 1.6h7.8a2 2 0 0 0 2-1.6L21 7H6" />
              <circle cx="10" cy="20" r="1" /><circle cx="18" cy="20" r="1" />
            </svg>
          </span>
          <span class="action-copy"><strong>闲置交易</strong><small>发现和发布校园好物</small></span>
          <span class="action-arrow" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M5 12h14M13 6l6 6-6 6" />
            </svg>
          </span>
        </router-link>

        <router-link to="/messages" class="action-card">
          <span class="action-icon blue">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
              <path d="M4 4h16a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z" />
              <path d="m22 6-10 7L2 6" />
            </svg>
          </span>
          <span class="action-copy"><strong>消息中心</strong><small>查看私信、好友与通知</small></span>
          <span class="action-arrow" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M5 12h14M13 6l6 6-6 6" />
            </svg>
          </span>
        </router-link>
      </div>
    </section>

    <section class="notice-card" aria-labelledby="notice-title">
      <div class="section-heading notice-heading">
        <div>
          <h2 id="notice-title">系统公告</h2>
          <p>平台更新与重要通知</p>
        </div>
        <span class="official-badge">官方</span>
      </div>

      <div v-if="loading" class="notice-state">加载中…</div>
      <div v-else-if="error" class="notice-state error">{{ error }}</div>
      <div v-else-if="announcements.length === 0" class="notice-empty">
        <span class="notice-icon">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
            <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" /><path d="M10 21h4" />
          </svg>
        </span>
        <div><strong>暂无新公告</strong><p>有新的平台动态时，会在这里通知你。</p></div>
      </div>
      <div v-else class="announcements">
        <article v-for="announcement in announcements" :key="announcement.id" class="announcement-item">
          <div><h3>{{ announcement.title }}</h3><p>{{ announcement.content }}</p></div>
          <time>{{ formatDate(announcement.date) }}</time>
        </article>
      </div>
    </section>
  </div>
</template>

<script setup>
import { computed, ref, onMounted } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { getAnnouncements } from '../../api'

const authStore = useAuthStore()
const announcements = ref([])
const loading = ref(false)
const error = ref(null)

const displayName = computed(() =>
  authStore.user?.username || '同学'
)

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

onMounted(async () => {
  loading.value = true
  try {
    const res = await getAnnouncements({ page: 1, pageSize: 10 })
    announcements.value = res.data.items || []
  } catch (e) {
    error.value = '加载公告失败'
  } finally {
    loading.value = false
  }
})
</script>

<style scoped>
.home-page { width:100%; max-width:1180px; margin:0 auto; }
.welcome-header { display:flex; align-items:flex-end; justify-content:space-between; gap:2rem; padding:1rem 0 2rem; border-bottom:1px solid var(--border); }
.welcome-kicker { margin-bottom:.35rem; color:var(--primary); font-size:.75rem; font-weight:700; letter-spacing:.08em; }
.welcome-header h1 { color:#172033; font-size:clamp(2rem,4vw,3rem); line-height:1.15; letter-spacing:-.04em; }
.welcome-copy { margin-top:.65rem; color:var(--text-secondary); }
.primary-action { display:inline-flex; align-items:center; gap:.55rem; flex:0 0 auto; padding:.72rem 1rem; border-radius:10px; color:#fff; background:var(--primary); font-size:.875rem; font-weight:650; text-decoration:none; }
.primary-action:hover { background:var(--primary-hover); }
.primary-action svg { width:17px; height:17px; }
.workspace { padding:2rem 0; }
.section-heading { display:flex; align-items:center; justify-content:space-between; margin-bottom:1rem; }
.section-heading h2 { font-size:1.05rem; }
.section-heading p { margin-top:.15rem; color:var(--text-secondary); font-size:.82rem; }
.action-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:.85rem; }
.action-card { min-width:0; display:flex; align-items:center; gap:.85rem; padding:1rem; border:1px solid var(--border); border-radius:12px; color:var(--text); background:var(--surface); text-decoration:none; transition:border-color .2s, box-shadow .2s, transform .2s; }
.action-card:hover { transform:translateY(-2px); border-color:#c9c5f7; box-shadow:0 8px 22px rgba(31,35,55,.07); }
.action-icon { width:40px; height:40px; display:grid; place-items:center; flex:0 0 auto; border-radius:10px; }
.action-icon svg { width:20px; height:20px; }
.action-icon.purple { color:#5d4fd5; background:#efedff; }
.action-icon.teal { color:#0f817c; background:#e7f8f5; }
.action-icon.blue { color:#2563a8; background:#eaf3ff; }
.action-copy { min-width:0; display:flex; flex:1; flex-direction:column; }
.action-copy strong { font-size:.92rem; }
.action-copy small { overflow:hidden; margin-top:.15rem; color:var(--text-secondary); font-size:.76rem; text-overflow:ellipsis; white-space:nowrap; }
.action-arrow { width:28px; height:28px; display:grid; place-items:center; flex:0 0 auto; border-radius:8px; color:#7c8493; }
.action-arrow svg { width:16px; height:16px; }
.action-card:hover .action-arrow { color:var(--primary); background:#f2f0ff; }
.notice-card { padding:1.25rem; border:1px solid var(--border); border-radius:14px; background:var(--surface); }
.notice-heading { padding-bottom:1rem; border-bottom:1px solid var(--border); }
.official-badge { padding:.25rem .55rem; border-radius:999px; color:#5749c8; background:#efedff; font-size:.7rem; font-weight:700; }
.notice-empty, .notice-state { min-height:130px; display:flex; align-items:center; justify-content:center; gap:.8rem; color:var(--text-secondary); text-align:left; }
.notice-empty strong { color:var(--text); font-size:.9rem; }
.notice-empty p { margin-top:.15rem; font-size:.8rem; }
.notice-icon { width:42px; height:42px; display:grid; place-items:center; flex:0 0 auto; border-radius:50%; color:#7c8493; background:#f1f3f6; }
.notice-icon svg { width:21px; height:21px; }
.notice-state.error { color:#b42318; }
.announcement-item { display:flex; justify-content:space-between; gap:1rem; padding:1rem 0; border-bottom:1px solid var(--border); }
.announcement-item:last-child { border-bottom:0; }
.announcement-item h3 { font-size:.9rem; }
.announcement-item p, .announcement-item time { color:var(--text-secondary); font-size:.78rem; }
@media(max-width:760px){.welcome-header{align-items:flex-start;flex-direction:column;padding-top:.25rem}.primary-action{width:100%;justify-content:center}.action-grid{grid-template-columns:1fr}.workspace{padding:1.5rem 0}.action-copy small{white-space:normal}}
</style>
