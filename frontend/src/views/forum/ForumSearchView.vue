<template>
  <div class="forum-search">
    <header class="forum-topbar">
      <h1>搜索</h1>
      <div class="forum-topbar-actions">
        <label class="search-field">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
            <circle cx="11" cy="11" r="7" /><path d="m20 20-3.6-3.6" />
          </svg>
          <input v-model="searchInput" type="search" placeholder="搜索版块或帖子" @keyup.enter="applySearch" />
        </label>
        <button class="search-submit" type="button" @click="applySearch">搜索</button>
      </div>
    </header>

    <div v-if="!keyword" class="search-placeholder">
      <p>输入关键词，搜索相关版块和帖子</p>
    </div>

    <template v-else>
      <section class="search-section">
        <h2 class="section-title">版块 <span class="section-count">{{ matchedForums.length }}</span></h2>
        <div v-if="matchedForums.length" class="board-grid">
          <button
            v-for="forum in matchedForums"
            :key="forum.forumID"
            type="button"
            class="board-card"
            @click="goBoard(forum.forumID)"
          >
            <span class="board-avatar">{{ forumInitial(forum) }}</span>
            <span class="board-info">
              <span class="board-name">{{ forum.forumName }}</span>
              <span class="board-desc">{{ forum.description || '这个版块还没有简介' }}</span>
            </span>
            <span class="board-count">{{ forum.postCount || 0 }} 帖</span>
          </button>
        </div>
        <div v-else class="search-empty-inline">未找到相关版块</div>
      </section>

      <section class="search-section">
        <h2 class="section-title">帖子 <span v-if="!postsLoading" class="section-count">{{ totalPosts }}</span></h2>
        <div class="tieba-panel">
          <div v-if="postsLoading" class="loading">加载中...</div>
          <div v-else class="tieba-list">
            <ForumPostCard
              v-for="post in posts"
              :key="post.postID"
              :post="post"
              :show-forum="true"
              @open="goPostDetail"
              @like="handleLike"
              @favorite="handleFavorite"
              @report="openReport"
            />
            <div v-if="posts.length === 0" class="empty-state">
              <p>未找到相关帖子</p>
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
      </section>
    </template>
  </div>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ForumPostCard from '../../components/forum/ForumPostCard.vue'
import { useForum } from '../../composables/useForum'
import { getPosts } from '../../api'

const { forums, error, handleLike, handleFavorite, openReport, registerFeed } = useForum()

const route = useRoute()
const router = useRouter()

const goPostDetail = (post) => router.push(`/forums/post/${post.postID}`)
const goBoard = (forumId) => router.push(`/forums/board/${forumId}`)

const PAGE_SIZE = 20

const readQueryKeyword = () => (typeof route.query.q === 'string' ? route.query.q : '')

const searchInput = ref(readQueryKeyword())
const keyword = ref(searchInput.value.trim())

const posts = ref([])
const totalPosts = ref(0)
const postsLoading = ref(false)
const page = ref(1)

const matchedForums = computed(() => {
  const q = keyword.value.toLowerCase()
  if (!q) return []
  return forums.value.filter(forum =>
    (forum.forumName || '').toLowerCase().includes(q) ||
    (forum.description || '').toLowerCase().includes(q)
  )
})

const forumInitial = (forum) =>
  (forum.forumName || '版')[0]?.toUpperCase() || '版'

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
  if (!keyword.value) {
    posts.value = []
    totalPosts.value = 0
    return
  }

  try {
    postsLoading.value = true
    const res = await getPosts({
      keyword: keyword.value,
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
    error.value = '无法加载搜索结果: ' + (e.response?.data?.message || e.message)
  } finally {
    postsLoading.value = false
  }
}

const runSearch = async () => {
  page.value = 1
  await loadPosts()
}

const applySearch = () => {
  const q = searchInput.value.trim()
  if (q === readQueryKeyword()) {
    keyword.value = q
    runSearch()
    return
  }
  router.push({ path: '/forums/search', query: q ? { q } : {} })
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

// 从其他页面带新的 q 参数回到本页（组件被 keep-alive 缓存）时重新搜索
watch(() => route.query.q, (next) => {
  const q = typeof next === 'string' ? next : ''
  searchInput.value = q
  keyword.value = q.trim()
  runSearch()
})

let unregisterFeed = null

onMounted(() => {
  unregisterFeed = registerFeed({ reload: loadPosts, applyPostPatch })
  runSearch()
})

onUnmounted(() => {
  unregisterFeed?.()
})
</script>

<style scoped>
.forum-search {
  --tieba-blue: #2f7ee0;
  --tieba-blue-dark: #266bc4;
  --tieba-blue-light: #e6f1fc;
  min-width: 0;
}

.forum-topbar { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin-bottom: .55rem; }
.forum-topbar h1 { margin: 0; color: var(--forum-ink, #11172a); font-size: 1.55rem; letter-spacing: -.04em; }
.forum-topbar-actions { display: flex; align-items: center; gap: .55rem; }
.forum-topbar .search-field { width: min(360px, 35vw); }

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

.search-placeholder {
  margin-top: .9rem;
  padding: 4rem 1rem;
  border: 1px solid #e4e7ec;
  border-radius: 14px;
  background: #fff;
  color: var(--text-secondary);
  text-align: center;
}

.search-section { margin-top: .9rem; }

.section-title {
  display: flex;
  align-items: baseline;
  gap: .45rem;
  margin: 0 0 .6rem;
  color: #171d2e;
  font-size: 1rem;
}

.section-count {
  color: var(--tieba-blue);
  font-size: .78rem;
  font-weight: 700;
}

.board-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: .7rem;
}

.board-card {
  display: flex;
  align-items: center;
  gap: .7rem;
  padding: .8rem .9rem;
  border: 1px solid #e4e8ef;
  border-radius: 12px;
  background: #fff;
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
  transition: border-color .15s ease, background .15s ease;
}

.board-card:hover {
  border-color: var(--tieba-blue);
  background: var(--tieba-blue-light);
}

.board-avatar {
  width: 40px;
  height: 40px;
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  border-radius: 8px;
  color: #fff;
  background: linear-gradient(135deg, #4d9bf0, #2f7ee0);
  font-size: 1rem;
  font-weight: 800;
}

.board-info {
  display: grid;
  gap: .15rem;
  min-width: 0;
  flex: 1;
}

.board-name {
  overflow: hidden;
  color: #171d2e;
  font-size: .86rem;
  font-weight: 700;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.board-card:hover .board-name {
  color: var(--tieba-blue);
}

.board-desc {
  overflow: hidden;
  color: var(--text-secondary);
  font-size: .72rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.board-count {
  flex: 0 0 auto;
  color: #9aa2b0;
  font-size: .72rem;
}

.search-empty-inline {
  padding: 1rem .2rem;
  border: 1px dashed #dfe2e8;
  border-radius: 12px;
  color: var(--text-secondary);
  font-size: .8rem;
  text-align: center;
}

.tieba-panel {
  border: 1px solid #e4e7ec;
  border-radius: 14px;
  background: #fff;
  overflow: hidden;
}

.tieba-list {
  display: flex;
  flex-direction: column;
}

.loading {
  padding: 3rem 1rem;
  color: var(--text-secondary);
  text-align: center;
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

@media (max-width: 640px) {
  .forum-topbar { align-items: stretch; flex-direction: column; }
  .forum-topbar-actions { width: 100%; }
  .forum-topbar .search-field { width: auto; flex: 1; }
}
</style>
