<template>
  <div class="home-page">
    <section class="hero" aria-labelledby="home-title">
      <div class="hero-orb hero-orb-one" aria-hidden="true"></div>
      <div class="hero-orb hero-orb-two" aria-hidden="true"></div>

      <div class="hero-copy">
        <div class="eyebrow">
          <span class="status-dot"></span>
          TONGJI CAMPUS HUB
        </div>
        <h1 id="home-title">
          欢迎回来，<br>
          <span>{{ displayName }}</span>
        </h1>
        <p>发现校园新鲜事，交换闲置好物，也和志同道合的同学保持连接。</p>
        <div class="hero-actions">
          <router-link to="/forums" class="hero-button hero-button-primary">
            逛逛校园论坛
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
              <path d="M5 12h14M13 6l6 6-6 6" />
            </svg>
          </router-link>
          <router-link to="/products" class="hero-button hero-button-secondary">发现闲置好物</router-link>
        </div>
      </div>

      <div class="hero-dashboard" aria-label="社区功能概览">
        <div class="dashboard-heading">
          <div>
            <span>COMMUNITY PULSE</span>
            <strong>校园生活，一站抵达</strong>
          </div>
          <div class="pulse-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
              <path d="M3 12h4l2-7 4 14 2-7h6" />
            </svg>
          </div>
        </div>
        <div class="dashboard-list">
          <div class="dashboard-row">
            <span class="row-icon row-icon-violet">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
              </svg>
            </span>
            <span><strong>校园讨论</strong><small>观点、经验与即时分享</small></span>
            <span class="row-state">开放中</span>
          </div>
          <div class="dashboard-row">
            <span class="row-icon row-icon-cyan">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                <path d="M3 3h2l2.4 11.2a2 2 0 0 0 2 1.6h7.8a2 2 0 0 0 2-1.6L21 7H6" />
                <circle cx="10" cy="20" r="1" /><circle cx="18" cy="20" r="1" />
              </svg>
            </span>
            <span><strong>闲置交易</strong><small>让每件好物继续流转</small></span>
            <span class="row-state">安全保障</span>
          </div>
          <div class="dashboard-row">
            <span class="row-icon row-icon-orange">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                <circle cx="12" cy="12" r="9" /><path d="M8 12l2.5 2.5L16 9" />
              </svg>
            </span>
            <span><strong>可信社区</strong><small>校园身份与信用体系守护</small></span>
            <span class="row-state">已连接</span>
          </div>
        </div>
      </div>
    </section>

    <section class="quick-section" aria-labelledby="quick-title">
      <div class="section-heading">
        <div>
          <span class="section-kicker">QUICK START</span>
          <h2 id="quick-title">今天想做什么？</h2>
        </div>
        <p>常用功能无需寻找，从这里直接开始。</p>
      </div>

      <div class="action-grid">
        <router-link to="/forums" class="action-card action-card-featured">
          <span class="action-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
              <path d="M21 15a2 2 0 0 1-2 2H8l-5 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
              <path d="M8 9h8M8 13h5" />
            </svg>
          </span>
          <span class="action-copy">
            <small>FORUM</small>
            <strong>加入校园热议</strong>
            <span>分享观点、提出问题，看看同学们正在关注什么。</span>
          </span>
          <span class="action-arrow" aria-hidden="true">↗</span>
        </router-link>

        <router-link to="/products" class="action-card action-card-market">
          <span class="action-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
              <path d="M6 2l1 4h10l1-4M4 6h16l-1 15H5L4 6Z" /><path d="M9 10v1a3 3 0 0 0 6 0v-1" />
            </svg>
          </span>
          <span class="action-copy">
            <small>MARKET</small>
            <strong>探索闲置市场</strong>
            <span>淘到心仪好物，或发布你的闲置。</span>
          </span>
          <span class="action-arrow" aria-hidden="true">↗</span>
        </router-link>

        <router-link to="/finance" class="action-card action-card-finance">
          <span class="action-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
              <rect x="3" y="5" width="18" height="14" rx="3" /><path d="M3 10h18M16 15h2" />
            </svg>
          </span>
          <span class="action-copy">
            <small>WALLET</small>
            <strong>查看资金流水</strong>
            <span>余额、冻结金额与每笔交易清晰可查。</span>
          </span>
          <span class="action-arrow" aria-hidden="true">↗</span>
        </router-link>

        <router-link to="/profile" class="action-card action-card-profile">
          <span class="action-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
              <circle cx="12" cy="8" r="4" /><path d="M4 21a8 8 0 0 1 16 0" />
            </svg>
          </span>
          <span class="action-copy">
            <small>PROFILE</small>
            <strong>完善个人主页</strong>
            <span>管理资料，了解你的社区信用记录。</span>
          </span>
          <span class="action-arrow" aria-hidden="true">↗</span>
        </router-link>
      </div>
    </section>

    <section class="content-grid">
      <article class="content-panel announcement-panel">
        <div class="panel-heading">
          <div>
            <span class="section-kicker">NOTICE BOARD</span>
            <h2>系统公告</h2>
          </div>
          <span class="panel-badge">官方</span>
        </div>

        <div v-if="loading" class="panel-state">加载中...</div>
        <div v-else-if="error" class="panel-state panel-state-error">{{ error }}</div>
        <div v-else-if="announcements.length === 0" class="empty-notice">
          <span class="empty-notice-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
              <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" /><path d="M10 21h4" />
            </svg>
          </span>
          <div><strong>暂无新公告</strong><p>有新的平台动态时，我们会第一时间在这里通知你。</p></div>
        </div>
        <div v-else class="announcements">
          <div v-for="announcement in announcements" :key="announcement.id" class="announcement-item">
            <div><h3>{{ announcement.title }}</h3><p>{{ announcement.content }}</p></div>
            <time>{{ announcement.date }}</time>
          </div>
        </div>
      </article>

      <article class="content-panel about-panel">
        <div class="about-mark">同济</div>
        <span class="section-kicker section-kicker-light">BUILT FOR TONGJI</span>
        <h2>让校园连接<br>更简单一点。</h2>
        <p>这是一个面向同济师生的交流与交易平台。我们希望每一次发言、交换和互助，都可靠且有温度。</p>
        <div class="about-tags">
          <span>真实校园身份</span><span>社区信用守护</span><span>便捷同学互助</span>
        </div>
        <div class="about-contact">
          <span>联系团队</span>
          <a href="mailto:2352580@tongji.edu.cn">2352580@tongji.edu.cn</a>
        </div>
      </article>
    </section>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { useAuthStore } from '../stores/auth'

const authStore = useAuthStore()
const announcements = ref([])
const loading = ref(false)
const error = ref(null)

const displayName = computed(() =>
  authStore.user?.nickname || authStore.user?.username || '同学'
)
</script>

<style scoped>
.home-page {
  --home-ink: #10172a;
  --home-purple: #6957f5;
  --home-purple-dark: #5040d9;
  width: 100%;
  max-width: 1480px;
  margin: 0 auto;
  color: var(--home-ink);
}

.hero {
  position: relative;
  isolation: isolate;
  min-height: 390px;
  display: grid;
  grid-template-columns: minmax(0, 1.18fr) minmax(360px, 0.82fr);
  align-items: center;
  gap: clamp(2rem, 5vw, 5rem);
  padding: clamp(2.25rem, 5vw, 4.75rem);
  overflow: hidden;
  border-radius: 32px;
  background:
    radial-gradient(circle at 18% 0%, rgba(164, 144, 255, 0.38), transparent 32%),
    linear-gradient(135deg, #211d51 0%, #342875 48%, #6249dc 100%);
  color: #fff;
  box-shadow: 0 30px 70px rgba(51, 38, 119, 0.22);
}

.hero::before {
  content: '';
  position: absolute;
  inset: 0;
  z-index: -2;
  opacity: 0.16;
  background-image: linear-gradient(rgba(255,255,255,.16) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.16) 1px, transparent 1px);
  background-size: 42px 42px;
  mask-image: linear-gradient(to right, black, transparent 72%);
}

.hero-orb {
  position: absolute;
  z-index: -1;
  border-radius: 50%;
  filter: blur(2px);
  pointer-events: none;
}

.hero-orb-one { width: 290px; height: 290px; right: -110px; top: -130px; background: rgba(91, 223, 255, .2); }
.hero-orb-two { width: 180px; height: 180px; left: 42%; bottom: -120px; background: rgba(255, 176, 117, .18); }

.hero-copy { position: relative; z-index: 1; }

.eyebrow {
  display: inline-flex;
  align-items: center;
  gap: .65rem;
  margin-bottom: 1.5rem;
  font-size: .72rem;
  font-weight: 800;
  letter-spacing: .18em;
  color: #d9d4ff;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #6fffc0;
  box-shadow: 0 0 0 6px rgba(111, 255, 192, .12), 0 0 22px rgba(111, 255, 192, .8);
}

.hero h1 {
  max-width: 720px;
  font-size: clamp(2.65rem, 5vw, 5rem);
  line-height: .98;
  letter-spacing: -.055em;
  font-weight: 800;
}

.hero h1 span { color: #d4cbff; }

.hero-copy > p {
  max-width: 600px;
  margin-top: 1.5rem;
  font-size: clamp(1rem, 1.4vw, 1.18rem);
  line-height: 1.8;
  color: rgba(255,255,255,.72);
}

.hero-actions { display: flex; flex-wrap: wrap; gap: .8rem; margin-top: 2rem; }

.hero-button {
  min-height: 48px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: .65rem;
  padding: .8rem 1.25rem;
  border-radius: 14px;
  text-decoration: none;
  font-size: .9rem;
  font-weight: 700;
  transition: transform .22s ease, background .22s ease, box-shadow .22s ease;
}

.hero-button:hover { transform: translateY(-2px); }
.hero-button svg { width: 18px; height: 18px; }
.hero-button-primary { color: #342875; background: #fff; box-shadow: 0 12px 30px rgba(12, 9, 43, .22); }
.hero-button-primary:hover { background: #f2efff; box-shadow: 0 16px 35px rgba(12, 9, 43, .3); }
.hero-button-secondary { color: #fff; border: 1px solid rgba(255,255,255,.28); background: rgba(255,255,255,.09); backdrop-filter: blur(12px); }
.hero-button-secondary:hover { background: rgba(255,255,255,.16); }

.hero-dashboard {
  position: relative;
  z-index: 1;
  padding: 1.1rem;
  border: 1px solid rgba(255,255,255,.18);
  border-radius: 24px;
  background: rgba(11, 9, 39, .36);
  box-shadow: 0 22px 55px rgba(9, 6, 39, .24);
  backdrop-filter: blur(22px);
}

.dashboard-heading { display: flex; align-items: center; justify-content: space-between; padding: .55rem .65rem 1.1rem; }
.dashboard-heading > div:first-child { display: flex; flex-direction: column; gap: .25rem; }
.dashboard-heading span { color: #aea4ed; font-size: .64rem; letter-spacing: .13em; font-weight: 800; }
.dashboard-heading strong { font-size: 1rem; }
.pulse-icon { width: 40px; height: 40px; display: grid; place-items: center; color: #aef7da; border-radius: 12px; background: rgba(104, 255, 198, .11); }
.pulse-icon svg { width: 20px; }
.dashboard-list { display: grid; gap: .55rem; }
.dashboard-row { min-width: 0; display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: .8rem; padding: .9rem; border-radius: 16px; background: rgba(255,255,255,.075); }
.row-icon { width: 40px; height: 40px; display: grid; place-items: center; border-radius: 12px; }
.row-icon svg { width: 20px; height: 20px; }
.row-icon-violet { color: #d2c8ff; background: rgba(151, 127, 255, .18); }
.row-icon-cyan { color: #a8efff; background: rgba(83, 207, 239, .15); }
.row-icon-orange { color: #ffd1a8; background: rgba(255, 164, 93, .15); }
.dashboard-row > span:nth-child(2) { min-width: 0; display: flex; flex-direction: column; }
.dashboard-row strong { font-size: .88rem; }
.dashboard-row small { overflow: hidden; color: rgba(255,255,255,.54); font-size: .72rem; text-overflow: ellipsis; white-space: nowrap; }
.row-state { padding: .3rem .55rem; border-radius: 999px; color: #c7ffe5; background: rgba(103,255,189,.1); font-size: .62rem; font-weight: 700; white-space: nowrap; }

.quick-section { padding: clamp(3rem, 6vw, 5.25rem) 0 0; }
.section-heading { display: flex; justify-content: space-between; align-items: end; gap: 2rem; margin-bottom: 1.6rem; }
.section-heading > div { display: flex; flex-direction: column; gap: .35rem; }
.section-kicker { color: var(--home-purple); font-size: .65rem; font-weight: 800; letter-spacing: .17em; }
.section-heading h2, .panel-heading h2 { font-size: clamp(1.65rem, 2.5vw, 2.3rem); line-height: 1.15; letter-spacing: -.035em; }
.section-heading > p { max-width: 340px; color: var(--text-secondary); font-size: .9rem; text-align: right; }

.action-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 1rem; }
.action-card {
  position: relative;
  min-height: 260px;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  overflow: hidden;
  padding: 1.5rem;
  border: 1px solid rgba(32, 37, 62, .08);
  border-radius: 24px;
  color: var(--home-ink);
  text-decoration: none;
  background: #fff;
  box-shadow: 0 12px 35px rgba(29, 35, 58, .06);
  transition: transform .25s ease, box-shadow .25s ease, border-color .25s ease;
}

.action-card::after { content: ''; position: absolute; right: -30px; bottom: -45px; width: 150px; height: 150px; border-radius: 50%; background: var(--card-glow); opacity: .38; filter: blur(2px); transition: transform .3s ease; }
.action-card:hover { transform: translateY(-6px); border-color: rgba(105, 87, 245, .25); box-shadow: 0 24px 55px rgba(45, 39, 99, .13); }
.action-card:hover::after { transform: scale(1.18); }
.action-card-featured { --card-glow: #b9aaff; background: linear-gradient(150deg, #f3f0ff 0%, #fff 72%); }
.action-card-market { --card-glow: #9de9ec; background: linear-gradient(150deg, #edfcfb 0%, #fff 72%); }
.action-card-finance { --card-glow: #ffd39e; background: linear-gradient(150deg, #fff8eb 0%, #fff 72%); }
.action-card-profile { --card-glow: #a9d5ff; background: linear-gradient(150deg, #eef7ff 0%, #fff 72%); }
.action-icon { position: relative; z-index: 1; width: 52px; height: 52px; display: grid; place-items: center; margin-bottom: auto; border-radius: 16px; color: var(--home-purple); background: rgba(255,255,255,.82); box-shadow: 0 8px 24px rgba(40, 35, 77, .09); }
.action-icon svg { width: 24px; height: 24px; }
.action-copy { position: relative; z-index: 1; display: flex; flex-direction: column; margin-top: 2rem; }
.action-copy small { margin-bottom: .35rem; color: var(--home-purple); font-size: .62rem; font-weight: 800; letter-spacing: .15em; }
.action-copy strong { font-size: 1.13rem; letter-spacing: -.02em; }
.action-copy > span { margin-top: .45rem; color: var(--text-secondary); font-size: .82rem; line-height: 1.65; }
.action-arrow { position: absolute; z-index: 1; top: 1.5rem; right: 1.5rem; color: #9a9fb2; font-size: 1.15rem; transition: transform .22s ease, color .22s ease; }
.action-card:hover .action-arrow { transform: translate(3px, -3px); color: var(--home-purple); }

.content-grid { display: grid; grid-template-columns: minmax(0, 1.25fr) minmax(350px, .75fr); gap: 1rem; padding: 1rem 0 3rem; }
.content-panel { min-height: 340px; padding: clamp(1.5rem, 3vw, 2.25rem); border-radius: 26px; }
.announcement-panel { border: 1px solid var(--border); background: #fff; box-shadow: 0 12px 35px rgba(29, 35, 58, .045); }
.panel-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
.panel-heading > div { display: flex; flex-direction: column; gap: .35rem; }
.panel-badge { padding: .35rem .7rem; border-radius: 999px; color: #5747db; background: #efedff; font-size: .7rem; font-weight: 700; }
.panel-state, .empty-notice { min-height: 205px; display: flex; align-items: center; justify-content: center; }
.panel-state { color: var(--text-secondary); }
.panel-state-error { color: #b42318; }
.empty-notice { flex-direction: column; text-align: center; }
.empty-notice-icon { width: 58px; height: 58px; display: grid; place-items: center; margin-bottom: 1rem; border-radius: 18px; color: var(--home-purple); background: #f1efff; }
.empty-notice-icon svg { width: 27px; height: 27px; }
.empty-notice strong { font-size: 1rem; }
.empty-notice p { max-width: 350px; margin-top: .4rem; color: var(--text-secondary); font-size: .82rem; line-height: 1.6; }
.announcements { display: grid; gap: .75rem; margin-top: 1.5rem; }
.announcement-item { display: flex; justify-content: space-between; gap: 1.5rem; padding: 1rem; border-radius: 16px; background: var(--bg); }
.announcement-item h3 { margin-bottom: .3rem; font-size: .95rem; }
.announcement-item p, .announcement-item time { color: var(--text-secondary); font-size: .78rem; }
.announcement-item time { white-space: nowrap; }

.about-panel { position: relative; overflow: hidden; display: flex; flex-direction: column; align-items: flex-start; color: #fff; background: #12182a; }
.about-panel::after { content: ''; position: absolute; right: -80px; bottom: -100px; width: 270px; height: 270px; border-radius: 50%; background: linear-gradient(135deg, #745ff1, #25c7ce); opacity: .33; filter: blur(4px); }
.about-mark { position: absolute; right: 1.8rem; top: 1.5rem; color: rgba(255,255,255,.06); font-size: 4.8rem; font-weight: 900; letter-spacing: -.12em; writing-mode: vertical-rl; }
.section-kicker-light { color: #9c91f9; }
.about-panel h2 { position: relative; z-index: 1; margin-top: 1rem; font-size: clamp(1.9rem, 3vw, 2.7rem); line-height: 1.08; letter-spacing: -.045em; }
.about-panel > p { position: relative; z-index: 1; max-width: 520px; margin-top: 1.2rem; color: rgba(255,255,255,.63); font-size: .88rem; line-height: 1.75; }
.about-tags { position: relative; z-index: 1; display: flex; flex-wrap: wrap; gap: .45rem; margin-top: 1.4rem; }
.about-tags span { padding: .42rem .65rem; border: 1px solid rgba(255,255,255,.11); border-radius: 999px; color: rgba(255,255,255,.74); background: rgba(255,255,255,.055); font-size: .69rem; }
.about-contact { position: relative; z-index: 1; width: 100%; display: flex; justify-content: space-between; gap: 1rem; margin-top: auto; padding-top: 1.5rem; color: rgba(255,255,255,.45); font-size: .7rem; }
.about-contact a { color: #fff; text-decoration: none; }
.about-contact a:hover { text-decoration: underline; }

@media (max-width: 1180px) {
  .hero { grid-template-columns: 1fr; }
  .hero-dashboard { max-width: 700px; }
  .action-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .action-card { min-height: 230px; }
}

@media (max-width: 820px) {
  .hero { padding: 2rem; border-radius: 24px; }
  .content-grid { grid-template-columns: 1fr; }
  .about-panel { min-height: 360px; }
}

@media (max-width: 640px) {
  .hero { min-height: auto; padding: 1.6rem; gap: 1.6rem; border-radius: 20px; }
  .hero h1 { font-size: clamp(2rem, 9vw, 2.6rem); line-height: 1.06; }
  .hero-copy > p { font-size: .92rem; }
  .hero-actions, .hero-button { width: 100%; }
  .hero-dashboard { padding: .75rem; border-radius: 18px; }
  .dashboard-row { grid-template-columns: auto minmax(0, 1fr); }
  .row-state { display: none; }
  .section-heading { align-items: flex-start; flex-direction: column; gap: .6rem; }
  .section-heading > p { text-align: left; }
  .action-grid { grid-template-columns: 1fr; }
  .action-card { min-height: 215px; }
  .content-panel { min-height: 320px; border-radius: 20px; }
  .about-contact { flex-direction: column; }
}

@media (prefers-reduced-motion: reduce) {
  .hero-button, .action-card, .action-card::after, .action-arrow { transition: none; }
}
</style>
