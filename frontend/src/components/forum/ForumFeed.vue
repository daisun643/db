<template>
  <div class="forum-feed">
    <header class="forum-topbar">
      <h1>论坛</h1>
      <div class="forum-topbar-actions">
        <label class="search-field">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
            <circle cx="11" cy="11" r="7" /><path d="m20 20-3.6-3.6" />
          </svg>
          <input v-model="searchInput" type="search" placeholder="搜索版块或帖子" @keyup.enter="applySearch" />
        </label>
        <button class="search-submit" type="button" @click="applySearch">搜索</button>
        <button class="compose-trigger" type="button" @click="openComposer">发帖</button>
      </div>
    </header>

    <div class="forum-content-grid">
      <div class="forum-feed-column">
        <div class="tieba-panel">

          <div v-if="loading" class="loading">加载中...</div>
          <div v-else class="tieba-list">
            <ForumPostCard
              v-for="post in displayPosts"
              :key="post.postID"
              :post="post"
              :show-forum="!forumId"
              @open="goPostDetail"
              @like="handleLike"
              @favorite="handleFavorite"
              @report="openReport"
            />
            <div v-if="posts.length === 0" class="empty-state">
              <p>暂无帖子</p>
            </div>
          </div>

          <div v-if="totalPages > 1" class="tieba-pagination">
            <button type="button" :disabled="page <= 1" @click="goToPage(page - 1)">上一页</button>
            <template v-for="item in pageItems" :key="item.key">
              <span v-if="item.type === 'gap'" class="page-gap">…</span>
              <button
                v-else
                type="button"
                :class="{ current: item.value === page }"
                @click="goToPage(item.value)"
              >{{ item.value }}</button>
            </template>
            <button type="button" :disabled="page >= totalPages" @click="goToPage(page + 1)">下一页</button>
          </div>
        </div>
      </div>
      <aside class="forum-rail">
        <section v-if="forum" class="rail-card forum-header">
          <img v-if="canShowAvatar(forum.avatarUrl)" :src="forum.avatarUrl" :alt="forum.forumName" class="forum-header-avatar forum-header-avatar-img" @error="markAvatarFailed(forum.avatarUrl)" />
          <span v-else class="forum-header-avatar">{{ forumInitial }}</span>
          <div class="forum-header-info">
            <div class="forum-title-row"><h2>{{ forum.forumName }}</h2><span class="forum-live-dot">活跃中</span></div>
            <p>{{ forum.description || '这个版块还没有简介' }}</p>
            <div class="forum-header-stats">
              <span>帖子数量 <b>{{ forum.postCount || 0 }}</b></span>
              <span>成员 <b>{{ forum.memberCount || 0 }}</b></span>
            </div>
            <button
              class="forum-join-btn"
              :class="{ joined: forum.isJoined }"
              type="button"
              :disabled="!authStore.isAuthenticated || togglingForumJoinId === forum.forumID"
              :title="!authStore.isAuthenticated ? '登录后可关注版块' : (forum.isJoined ? '点击取消关注' : '关注版块')"
              @click="handleToggleForumJoin(forum)"
            >{{ forum.isJoined ? '已关注' : '+ 关注版块' }}</button>
          </div>
        </section>
        <template v-else>
          <section class="rail-card forum-header">
            <span class="forum-header-avatar">全</span>
            <div class="forum-header-info">
              <div class="forum-title-row"><h2>全部版块</h2><span class="forum-live-dot">活跃中</span></div>
              <p>首页汇集所有版块的最新帖子，点击帖子左上角的版块标签即可进入对应版块。</p>
              <div class="forum-header-stats">
                <span>版块数量 <b>{{ forums.length }}</b></span>
                <span>帖子总数 <b>{{ totalForumPosts }}</b></span>
              </div>
            </div>
          </section>
        </template>
      </aside>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import ForumPostCard from './ForumPostCard.vue'
import { useForum } from '../../composables/useForum'
import { useAuthStore } from '../../stores/auth'
import { getPosts } from '../../api'
import { canShowAvatar, markAvatarFailed } from '../../utils/avatarFallback'

const props = defineProps({
  forumId: { type: Number, default: null },
  forum: { type: Object, default: null },
})

const {
  forums,
  error,
  openComposer,
  handleLike,
  handleFavorite,
  openReport,
  registerFeed,
  togglingForumJoinId,
  handleToggleForumJoin,
} = useForum()

const authStore = useAuthStore()

const router = useRouter()
const goPostDetail = (post) => router.push(`/forums/post/${post.postID}`)
const goBoard = (forumId) => router.push(`/forums/board/${forumId}`)

const PAGE_SIZE = 20

const posts = ref([])
const totalPosts = ref(0)
const loading = ref(true)
const page = ref(1)
const activeSort = ref('latest')
const searchInput = ref('')

const filters = ref({
  forumId: props.forumId,
})

const displayPosts = computed(() => {
  const list = [...posts.value]
  if (activeSort.value === 'hot') return list.sort((a, b) => (b.commentCount || 0) - (a.commentCount || 0))
  if (activeSort.value === 'elite') return list.filter(post => post.status === 'Elite' || post.status === 'Pinned')
  return list
})

const forumInitial = computed(() =>
  (props.forum?.forumName || '版')[0]?.toUpperCase() || '版'
)

const totalForumPosts = computed(() =>
  forums.value.reduce((sum, item) => sum + (item.postCount || 0), 0)
)

const hotForums = computed(() =>
  [...forums.value]
    .sort((a, b) => (b.postCount || 0) - (a.postCount || 0))
    .slice(0, 6)
)

const totalPages = computed(() => Math.max(1, Math.ceil(totalPosts.value / PAGE_SIZE)))

const pageItems = computed(() => {
  const total = totalPages.value
  const current = page.value
  const wanted = new Set([1, 2, total - 1, total, current - 1, current, current + 1])
  const visible = [...wanted]
    .filter(n => n >= 1 && n <= total)
    .sort((a, b) => a - b)

  const items = []
  let previous = 0
  for (const value of visible) {
    if (previous && value - previous > 1) {
      items.push({ type: 'gap', key: `gap-${previous}-${value}` })
    }
    items.push({ type: 'page', value, key: `page-${value}` })
    previous = value
  }
  return items
})

const loadPosts = async () => {
  try {
    loading.value = true
    const res = await getPosts({
      forumId: filters.value.forumId || undefined,
      page: page.value,
      pageSize: PAGE_SIZE,
    })
    posts.value = Array.isArray(res.data) ? res.data : []
    const totalFromHeader = Number(res.headers?.['x-total-count'])
    const hasTotalHeader = Number.isFinite(totalFromHeader)
    totalPosts.value = hasTotalHeader ? totalFromHeader : posts.value.length
  } catch (e) {
    posts.value = []
    totalPosts.value = 0
    error.value = '无法加载帖子数据: ' + (e.response?.data?.message || e.message)
  } finally {
    loading.value = false
  }
}

const applySearch = () => {
  const q = searchInput.value.trim()
  router.push(q ? { path: '/forums/search', query: { q } } : { path: '/forums/search' })
}

const goToPage = async (target) => {
  if (target < 1 || target > totalPages.value || target === page.value) return
  page.value = target
  await loadPosts()
}

const applyPostPatch = (postId, patch) => {
  const target = posts.value.find(item => item.postID === postId)
  if (target) {
    Object.assign(target, patch)
  }
}

watch(() => props.forumId, async (nextForumId) => {
  if (filters.value.forumId === nextForumId) return
  filters.value.forumId = nextForumId
  page.value = 1
  await loadPosts()
})

let unregisterFeed = null

onMounted(() => {
  unregisterFeed = registerFeed({ reload: loadPosts, applyPostPatch })
  loadPosts()
})

onUnmounted(() => {
  unregisterFeed?.()
})
</script>

<style scoped>
.forum-feed {
  --tieba-blue: #2f7ee0;
  --tieba-blue-dark: #266bc4;
  --tieba-blue-light: #e6f1fc;
  min-width: 0;
}

.forum-topbar { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin-bottom: .55rem; }
.forum-topbar h1 { margin: 0; color: var(--forum-ink, #11172a); font-size: 1.55rem; letter-spacing: -.04em; }
.forum-topbar-actions { display: flex; align-items: center; gap: .55rem; }
.forum-topbar .search-field { width: min(360px, 35vw); }
.forum-topbar .compose-trigger { white-space: nowrap; }

.search-field {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: .55rem;
  padding: 0 .75rem;
  border: 1px solid #dfe2e8;
  border-radius: 4px;
  background: #f8f9fb;
}

.search-field:focus-within {
  border-color: var(--tieba-blue);
  background: #fff;
  box-shadow: 0 0 0 3px rgba(47, 126, 224, .09);
}

.search-field svg {
  width: 17px;
  height: 17px;
  flex: 0 0 auto;
  color: #8b93a3;
}

.search-field input {
  width: 100%;
  min-width: 0;
  padding: .68rem 0;
  border: 0;
  border-radius: 0;
  background: transparent;
  outline: 0;
}

.search-submit {
  padding: .68rem 1rem;
  border: 0;
  border-radius: 4px;
  color: #fff;
  background: var(--tieba-blue);
  font: inherit;
  font-size: .82rem;
  font-weight: 650;
  cursor: pointer;
}

.search-submit:hover {
  background: var(--tieba-blue-dark);
}

.compose-trigger {
  align-items: center;
  background: var(--tieba-blue);
  border: none;
  border-radius: 4px;
  color: white;
  cursor: pointer;
  display: inline-flex;
  font: inherit;
  font-size: .82rem;
  font-weight: 700;
  gap: .35rem;
  justify-content: center;
  padding: .55rem 1.15rem;
  white-space: nowrap;
}

.compose-trigger:hover {
  background: var(--tieba-blue-dark);
}

.forum-header {
  display: grid;
  gap: .65rem;
  justify-items: start;
}

.forum-title-row { display: flex; align-items: center; gap: .55rem; }
.forum-live-dot { padding: .16rem .42rem; border-radius: 999px; color: #18794e; background: #e6f7ee; font-size: .62rem; font-weight: 700; }

.forum-header-avatar {
  width: 52px;
  height: 52px;
  display: grid;
  place-items: center;
  border-radius: 8px;
  color: #fff;
  background: linear-gradient(135deg, #4d9bf0, #2f7ee0);
  font-size: 1.3rem;
  font-weight: 800;
}

.forum-header-avatar-img {
  object-fit: cover;
}

.forum-header-info {
  display: grid;
  gap: .3rem;
  min-width: 0;
}

.forum-header-info h2 {
  margin: 0;
  color: #171d2e;
  font-size: .95rem;
}

.forum-header-info p {
  margin: 0;
  color: var(--text-secondary);
  font-size: .74rem;
  line-height: 1.55;
}

.forum-header-stats {
  color: var(--text-secondary);
  display: flex;
  flex-wrap: wrap;
  gap: .25rem .9rem;
  font-size: .74rem;
}

.forum-header-stats b {
  color: var(--tieba-blue);
  font-size: .84rem;
}

.forum-join-btn {
  justify-self: start;
  margin-top: .35rem;
  border: 0;
  border-radius: 999px;
  padding: .45rem .95rem;
  color: #fff;
  background: var(--tieba-blue);
  font: inherit;
  font-size: .76rem;
  font-weight: 750;
  cursor: pointer;
}

.forum-join-btn:hover:not(:disabled) {
  background: var(--tieba-blue-dark);
}

.forum-join-btn.joined {
  color: var(--tieba-blue-dark);
  background: #eef4ff;
  border: 1px solid #cfe2f7;
}

.forum-join-btn:disabled {
  opacity: .55;
  cursor: not-allowed;
}

.feed-toolbar {
  display: block;
  padding: 1.15rem;
  border: 1px solid #e4e7ec;
  border-radius: 14px;
  background: #fff;
}

.feed-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.feed-heading h2 {
  color: #171d2e;
  font-size: 1.05rem;
}

.feed-heading p {
  margin-top: .15rem;
  color: var(--text-secondary);
  font-size: .75rem;
}

.forum-content-grid { display: grid; grid-template-columns: minmax(0, 1fr) 230px; gap: 1rem; align-items: start; }
.forum-feed-column { min-width: 0; }
.forum-rail { display: grid; gap: 1rem; }
.rail-card { padding: 1rem; border: 1px solid #e4e8ef; border-radius: 14px; background: #fff; }
.rail-heading { display: flex; align-items: center; justify-content: space-between; margin-bottom: .75rem; color: #1b2742; font-size: .82rem; font-weight: 800; }
.rail-dot { width: 7px; height: 7px; border-radius: 50%; background: #41c98a; box-shadow: 0 0 0 4px #e8f8ef; }
.hot-topic { display: grid; grid-template-columns: 1.3rem 1fr; gap: .35rem; width: 100%; padding: .62rem 0; border: 0; border-bottom: 1px solid #eef0f4; color: inherit; background: transparent; text-align: left; cursor: pointer; }
.hot-topic:last-of-type { border-bottom: 0; }.hot-rank { color: #a1aabd; font-size: .72rem; font-weight: 800; }.hot-title { overflow: hidden; color: #45536b; font-size: .74rem; text-overflow: ellipsis; white-space: nowrap; }.hot-topic small { grid-column: 2; color: #a1aabd; font-size: .62rem; }.hot-topic:hover .hot-title { color: var(--tieba-blue); }.rail-empty { color: #9aa3b3; font-size: .75rem; }.rail-rule p { color: #778398; font-size: .72rem; line-height: 1.7; }.rule-link { display: inline-block; margin-top: .7rem; color: var(--tieba-blue); font-size: .7rem; font-weight: 700; }

.tieba-panel {
  margin-top: .9rem;
  border: 1px solid #e4e7ec;
  border-radius: 14px;
  background: #fff;
  overflow: hidden;
}

.sort-tabs {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  padding: 0 1.1rem;
  border-bottom: 1px solid #eceef2;
}

.sort-label { margin-right: auto; color: #1b2742; font-size: .78rem; font-weight: 800; }

.sort-tabs button {
  position: relative;
  border: 0;
  background: transparent;
  padding: .72rem .1rem;
  color: #5f6b7c;
  font: inherit;
  font-size: .85rem;
  font-weight: 600;
  cursor: pointer;
}

.sort-tabs button.active {
  color: var(--tieba-blue);
}

.sort-tabs button.active::after {
  content: '';
  position: absolute;
  left: 0;
  right: 0;
  bottom: -1px;
  height: 2px;
  background: var(--tieba-blue);
}

.tieba-list {
  display: flex;
  flex-direction: column;
}

.empty-state {
  padding: 3rem 1rem;
  color: var(--text-secondary);
  text-align: center;
}

.tieba-pagination {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  gap: .4rem;
  padding: .9rem 1rem 1.05rem;
  border-top: 1px solid #eceef2;
}

.tieba-pagination button {
  min-width: 34px;
  padding: .42rem .55rem;
  border: 1px solid #d9dee6;
  border-radius: 4px;
  color: #4c5567;
  background: #fff;
  font: inherit;
  font-size: .78rem;
  cursor: pointer;
}

.tieba-pagination button:hover:not(:disabled):not(.current) {
  color: var(--tieba-blue);
  border-color: var(--tieba-blue);
}

.tieba-pagination button.current {
  color: #fff;
  border-color: var(--tieba-blue);
  background: var(--tieba-blue);
  font-weight: 700;
}

.tieba-pagination button:disabled {
  color: #b6bcc7;
  cursor: not-allowed;
}

.page-gap {
  color: #9aa2b0;
  padding: 0 .15rem;
}

@media (max-width: 980px) {
  .forum-content-grid { grid-template-columns: 1fr; }
  .forum-rail { display: none; }
}

@media (max-width: 640px) {
  .forum-topbar { align-items: stretch; flex-direction: column; }
  .forum-topbar-actions { width: 100%; }
  .forum-topbar .search-field { width: auto; flex: 1; }

  .sort-tabs { gap: .8rem; overflow-x: auto; }
  .sort-tabs button { white-space: nowrap; }
}
</style>
