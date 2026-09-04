<template>
  <div class="tab-content joined-forums">
    <header class="jf-page-head">
      <div>
        <h2 class="jf-page-title">关注的版块</h2>
        <p class="jf-page-sub">
          汇总你关注的所有版块，点击卡片即可进入版块，也可以随时取消关注。
        </p>
      </div>
    </header>

    <div v-if="loading" class="loading">加载中...</div>

    <div v-else-if="joinedForums.length" class="jf-list">
      <article
        v-for="forum in joinedForums"
        :key="forum.forumID"
        class="jf-card"
        role="link"
        tabindex="0"
        :title="'进入 ' + forum.forumName + ' 版块'"
        @click="goBoard(forum)"
        @keydown.enter="goBoard(forum)"
      >
        <img
          v-if="canShowAvatar(forum.avatarUrl)"
          :src="forum.avatarUrl"
          :alt="forum.forumName + ' 版块头像'"
          class="jf-avatar"
          @error="markAvatarFailed(forum.avatarUrl)"
        />
        <span v-else class="jf-avatar jf-avatar-fallback" aria-hidden="true">
          {{ (forum.forumName || '版')[0] }}
        </span>

        <div class="jf-main">
          <div class="jf-name-row">
            <h3 class="jf-name">{{ forum.forumName }}</h3>
            <span :class="['jf-pill', forum.status === 'Active' ? 'jf-pill-on' : 'jf-pill-off']">
              <i class="jf-pill-dot" aria-hidden="true"></i>
              {{ forum.status === 'Active' ? '开放中' : '已停用' }}
            </span>
            <span v-if="forum.canManage" class="jf-pill jf-pill-manage">我管理</span>
          </div>
          <p class="jf-desc">{{ forum.description || '这个版块还没有简介' }}</p>
          <div class="jf-stats">
            <span>{{ forum.postCount || 0 }} 篇帖子</span>
            <span>{{ forum.memberCount || 0 }} 名成员</span>
          </div>
        </div>

        <button
          class="jf-leave-btn"
          type="button"
          :disabled="leavingId === forum.forumID"
          title="取消关注该版块"
          @click.stop="handleLeave(forum)"
        >{{ leavingId === forum.forumID ? '取消中...' : '取消关注' }}</button>
      </article>
    </div>

    <div v-else class="empty-state jf-empty">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true">
        <path d="M12 20.5 4.7 13a4.9 4.9 0 0 1 0-7 4.8 4.8 0 0 1 6.9 0l.4.4.4-.4a4.8 4.8 0 0 1 6.9 0 4.9 4.9 0 0 1 0 7Z" />
      </svg>
      <p>你还没有关注任何版块</p>
      <button class="jf-btn-primary" type="button" @click="goHome">
        去论坛首页逛逛
      </button>
    </div>
  </div>
</template>

<script setup>
import { onActivated, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useForum } from '../../composables/useForum'
import { getJoinedForums, leaveForum } from '../../api'
import { canShowAvatar, markAvatarFailed } from '../../utils/avatarFallback'

const { error, notice, loadForums } = useForum()

const router = useRouter()

const joinedForums = ref([])
const loading = ref(false)
const leavingId = ref(null)
const loaded = ref(false)

const loadJoinedForums = async ({ silent = false } = {}) => {
  if (!silent) loading.value = true
  try {
    error.value = null
    const res = await getJoinedForums()
    joinedForums.value = res.data
    loaded.value = true
  } catch (e) {
    error.value = '无法加载关注的版块: ' + (e.response?.data?.message || e.message)
  } finally {
    if (!silent) loading.value = false
  }
}

const handleLeave = async (forum) => {
  if (!forum?.forumID || leavingId.value) return
  if (!window.confirm(`确定要取消关注「${forum.forumName}」吗？取消后可随时在版块页重新关注。`)) return

  try {
    leavingId.value = forum.forumID
    error.value = null
    const res = await leaveForum(forum.forumID)
    joinedForums.value = joinedForums.value.filter(item => item.forumID !== forum.forumID)
    notice.value = res.data?.message || '已取消关注。'
    // 同步侧边栏等处的关注状态
    await loadForums()
  } catch (e) {
    error.value = '取消关注失败: ' + (e.response?.data?.message || e.message)
  } finally {
    leavingId.value = null
  }
}

const goBoard = (forum) => {
  if (forum?.forumID) router.push(`/forums/board/${forum.forumID}`)
}

const goHome = () => {
  router.push('/forums')
}

onMounted(() => {
  loadJoinedForums()
})

// keep-alive 缓存期间在其他页面（如版块页）关注/取关后，回到本页静默刷新
onActivated(() => {
  if (loaded.value) loadJoinedForums({ silent: true })
})
</script>

<style scoped>
.tab-content {
  min-height: 400px;
  min-width: 0;
}

.joined-forums {
  width: min(960px, 100%);
  margin: 0 auto;
  color: #11172a;
}

/* ---------- 页头 ---------- */

.jf-page-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1.25rem;
}

.jf-page-title {
  margin: 0;
  font-size: 1.35rem;
  letter-spacing: -.02em;
}

.jf-page-sub {
  margin-top: .3rem;
  color: #687286;
  font-size: .82rem;
}

/* ---------- 版块卡片列表 ---------- */

.jf-list {
  display: flex;
  flex-direction: column;
  gap: .75rem;
}

.jf-card {
  display: flex;
  align-items: center;
  gap: .9rem;
  padding: .9rem 1rem;
  border: 1px solid #e4e7ec;
  border-radius: 12px;
  background: #fff;
  cursor: pointer;
  transition: border-color .15s ease, box-shadow .15s ease;
}

.jf-card:hover {
  border-color: #bcd3f1;
  box-shadow: 0 4px 14px rgba(38, 107, 196, .08);
}

.jf-card:focus-visible {
  outline: 2px solid #266bc4;
  outline-offset: 2px;
}

.jf-avatar {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  object-fit: cover;
  flex-shrink: 0;
}

.jf-avatar-fallback {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: #e6f1fc;
  color: #266bc4;
  font-weight: 700;
  font-size: 1.05rem;
}

.jf-main {
  flex: 1;
  min-width: 0;
}

.jf-name-row {
  display: flex;
  align-items: center;
  gap: .5rem;
  flex-wrap: wrap;
}

.jf-name {
  margin: 0;
  font-size: .95rem;
}

.jf-desc {
  margin: .25rem 0 0;
  color: #687286;
  font-size: .8rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.jf-stats {
  display: flex;
  gap: 1rem;
  margin-top: .35rem;
  color: #8b95a9;
  font-size: .75rem;
}

/* ---------- 状态标记 ---------- */

.jf-pill {
  display: inline-flex;
  align-items: center;
  gap: .3rem;
  padding: .12rem .55rem;
  border-radius: 999px;
  font-size: .68rem;
  font-weight: 600;
  flex-shrink: 0;
}

.jf-pill-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.jf-pill-on {
  background: #e8f7ee;
  color: #1e8e50;
}

.jf-pill-on .jf-pill-dot {
  background: #22a05c;
}

.jf-pill-off {
  background: #f1f2f4;
  color: #7a8499;
}

.jf-pill-off .jf-pill-dot {
  background: #98a2b3;
}

.jf-pill-manage {
  background: #eef4ff;
  color: #266bc4;
}

/* ---------- 操作按钮 ---------- */

.jf-leave-btn {
  flex-shrink: 0;
  padding: .45rem .85rem;
  border: 1px solid #e4e7ec;
  border-radius: 8px;
  background: #fff;
  color: #7a8499;
  font-size: .78rem;
  cursor: pointer;
  transition: border-color .15s ease, color .15s ease, background .15s ease;
}

.jf-leave-btn:hover:not(:disabled) {
  border-color: #f0b6b6;
  background: #fdf3f3;
  color: #c0392b;
}

.jf-leave-btn:disabled {
  opacity: .6;
  cursor: not-allowed;
}

.jf-btn-primary {
  margin-top: .5rem;
  padding: .55rem 1.1rem;
  border: none;
  border-radius: 8px;
  background: #2f7ee0;
  color: #fff;
  font-size: .82rem;
  cursor: pointer;
}

.jf-btn-primary:hover {
  background: #266bc4;
}

/* ---------- 空状态 ---------- */

.jf-empty {
  border: 1px solid #e4e7ec;
  border-radius: 12px;
  background: #fff;
  padding: 3rem 1rem;
}

@media (max-width: 640px) {
  .jf-card {
    flex-wrap: wrap;
  }

  .jf-desc {
    white-space: normal;
  }
}
</style>
