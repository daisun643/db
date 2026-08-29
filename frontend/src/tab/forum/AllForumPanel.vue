<template>
  <div class="forum-layout">
    <aside class="forum-sidebar">
      <div class="forum-sidebar-heading">
        <div class="section-title">版块</div>
        <button class="create-forum-trigger" type="button" @click="$emit('open-forum-creator')">
          <span aria-hidden="true">＋</span> 创建版块
        </button>
      </div>
      <button
        :class="['forum-filter', { active: filters.forumId === null }]"
        @click="selectForum(null)"
      >
        全部帖子
      </button>
      <button
        v-for="forum in forums"
        :key="forum.forumID"
        :class="['forum-filter', { active: filters.forumId === forum.forumID }]"
        @click="selectForum(forum.forumID)"
      >
        <span class="forum-filter-name" :title="forum.forumName">{{ forum.forumName }}</span>
        <span class="forum-count">{{ forum.postCount || 0 }}</span>
      </button>
    </aside>

    <main class="forum-main">
      <section v-if="currentForum" class="forum-header">
        <span class="forum-header-avatar">{{ forumInitial }}</span>
        <div class="forum-header-info">
          <h2>{{ currentForum.forumName }}</h2>
          <p>{{ currentForum.description || '这个版块还没有简介' }}</p>
          <div class="forum-header-stats">
            <span>主题 <b>{{ currentForum.postCount || 0 }}</b></span>
          </div>
        </div>
        <button class="compose-trigger" @click="$emit('open-composer')">发帖</button>
      </section>

      <div v-else class="feed-toolbar">
        <div class="feed-heading">
          <div>
            <h2>全部帖子</h2>
            <p>共 {{ totalPosts }} 条帖子</p>
          </div>
          <button class="compose-trigger" @click="$emit('open-composer')">发帖</button>
        </div>
        <div class="toolbar">
          <label class="search-field">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
              <circle cx="11" cy="11" r="7" /><path d="m20 20-3.6-3.6" />
            </svg>
            <input v-model="filters.keyword" type="search" placeholder="搜索帖子标题或内容" @keyup.enter="applyFilters" />
          </label>
          <button class="filter-trigger" type="button" @click="filterDialogOpen = true">
            筛选
            <span v-if="filters.tags.length" class="filter-count">{{ filters.tags.length }}</span>
          </button>
          <button class="search-submit" @click="applyFilters">搜索</button>
        </div>
      </div>

      <div v-if="filterDialogOpen" class="filter-dialog-backdrop" @click.self="filterDialogOpen = false">
        <section class="filter-dialog" role="dialog" aria-modal="true" aria-labelledby="filter-dialog-title">
          <header class="filter-dialog-header">
            <div>
              <h3 id="filter-dialog-title">筛选</h3>
              <p>按标签和发布时间筛选帖子</p>
            </div>
            <button class="icon-button" type="button" aria-label="关闭筛选窗口" @click="filterDialogOpen = false">×</button>
          </header>
          <div class="filter-dialog-body">
            <fieldset class="tag-filter-field">
              <legend>标签</legend>
              <div v-if="tagStats.length" class="tag-checkbox-list">
                <label v-for="stat in tagStats" :key="stat.tagId" class="tag-checkbox">
                  <input v-model="filters.tags" type="checkbox" :value="stat.tagName" />
                  <span>#{{ stat.tagName }}</span>
                  <small>{{ stat.postCount }}</small>
                </label>
              </div>
              <p v-else class="tag-filter-empty">暂无可选标签</p>
            </fieldset>
            <div class="filter-time-grid">
              <label>
                开始时间
                <input v-model="filters.from" type="datetime-local" />
              </label>
              <label>
                结束时间
                <input v-model="filters.to" type="datetime-local" />
              </label>
            </div>
          </div>
          <footer class="filter-dialog-actions">
            <button class="btn" type="button" @click="resetFilters">清空筛选</button>
            <button class="btn btn-primary" type="button" @click="applyFiltersAndClose">应用筛选</button>
          </footer>
        </section>
      </div>

      <div class="tieba-panel">
        <div class="sort-tabs">
          <button type="button" class="active">最新</button>
        </div>

        <div v-if="loading" class="loading">加载中...</div>
        <div v-else class="tieba-list">
          <ForumPostCard
            v-for="post in posts"
            :key="post.postID"
            :post="post"
            @open="$emit('open-post', $event)"
            @like="$emit('like', $event)"
            @favorite="$emit('favorite', $event)"
            @report="$emit('report', $event)"
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
    </main>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import ForumPostCard from '../../components/forum/ForumPostCard.vue'
import { getPosts, getTagStats } from '../../api'

const props = defineProps({
  forums: { type: Array, default: () => [] },
})

const emit = defineEmits(['open-forum-creator', 'open-composer', 'open-post', 'like', 'favorite', 'report', 'error'])

const PAGE_SIZE = 20

const posts = ref([])
const totalPosts = ref(0)
const loading = ref(true)
const tagStats = ref([])
const filterDialogOpen = ref(false)
const page = ref(1)

const filters = ref({
  forumId: null,
  keyword: '',
  tags: [],
  from: '',
  to: '',
})

const currentForum = computed(() =>
  props.forums.find(f => f.forumID === filters.value.forumId) || null
)

const forumInitial = computed(() =>
  (currentForum.value?.forumName || '版')[0]?.toUpperCase() || '版'
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
      keyword: filters.value.keyword.trim() || undefined,
      tags: filters.value.tags.join(',') || undefined,
      tagOp: filters.value.tags.length ? 'or' : undefined,
      from: filters.value.from || undefined,
      to: filters.value.to || undefined,
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
    emit('error', '无法加载帖子数据: ' + (e.response?.data?.message || e.message))
  } finally {
    loading.value = false
  }
}

const loadTagStats = async () => {
  try {
    const res = await getTagStats()
    tagStats.value = res.data
  } catch (e) {
    // 静默跳过
  }
}

const selectForum = async (forumId) => {
  filters.value.forumId = forumId
  page.value = 1
  await loadPosts()
}

const applyFilters = async () => {
  page.value = 1
  await loadPosts()
}

const applyFiltersAndClose = async () => {
  await applyFilters()
  filterDialogOpen.value = false
}

const resetFilters = async () => {
  filters.value = {
    forumId: filters.value.forumId,
    keyword: '',
    tags: [],
    from: '',
    to: '',
  }
  page.value = 1
  await loadPosts()
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

defineExpose({ reload: loadPosts, selectForum, applyPostPatch })

onMounted(() => {
  loadPosts()
  loadTagStats()
})
</script>

<style scoped>
.forum-layout {
  --tieba-blue: #2f7ee0;
  --tieba-blue-dark: #266bc4;
  --tieba-blue-light: #e6f1fc;
  display: grid;
  grid-template-columns: 210px minmax(0, 1fr);
  gap: 1rem;
  align-items: start;
  justify-content: stretch;
}

.forum-sidebar {
  background: var(--surface);
  border: 1px solid #e4e7ec;
  border-radius: 6px;
  padding: .75rem;
  position: sticky;
  top: 1rem;
}

.section-title {
  padding: .45rem .65rem .55rem;
  color: #8990a0;
  font-size: .68rem;
  font-weight: 700;
  letter-spacing: .08em;
  text-transform: uppercase;
}

.forum-sidebar-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: .5rem;
  margin-bottom: .35rem;
}

.forum-sidebar-heading .section-title {
  margin: 0;
  padding: .35rem 0;
}

.create-forum-trigger {
  align-items: center;
  background: var(--tieba-blue);
  border: 0;
  border-radius: 4px;
  color: #fff;
  cursor: pointer;
  display: inline-flex;
  flex: 0 0 auto;
  font-size: .7rem;
  font-weight: 700;
  gap: 0.3rem;
  margin: 0;
  padding: .4rem .55rem;
  white-space: nowrap;
}

.create-forum-trigger:hover {
  background: var(--tieba-blue-dark);
}

.forum-filter {
  width: 100%;
  min-height: 40px;
  margin: .1rem 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border: none;
  background: transparent;
  border-radius: 4px;
  padding: .6rem .7rem;
  color: var(--text);
  font-size: .82rem;
  cursor: pointer;
  text-align: left;
}

.forum-filter:hover {
  background: #f2f7fd;
}

.forum-filter.active {
  color: var(--tieba-blue);
  background: var(--tieba-blue-light);
  font-weight: 700;
}

.forum-filter-name {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.forum-count {
  min-width: 25px;
  padding: .2rem .38rem;
  border-radius: 999px;
  background: rgba(47, 126, 224, .08);
  color: var(--text-secondary);
  font-size: 0.75rem;
  text-align: center;
}

.forum-main {
  min-width: 0;
}

.forum-header {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.1rem 1.2rem;
  border: 1px solid #e4e7ec;
  border-radius: 6px;
  background: #fff;
}

.forum-header-avatar {
  width: 58px;
  height: 58px;
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  border-radius: 6px;
  color: #fff;
  background: linear-gradient(135deg, #4d9bf0, #2f7ee0);
  font-size: 1.45rem;
  font-weight: 800;
}

.forum-header-info {
  flex: 1;
  min-width: 0;
}

.forum-header-info h2 {
  color: #171d2e;
  font-size: 1.15rem;
}

.forum-header-info p {
  margin-top: .25rem;
  overflow: hidden;
  color: var(--text-secondary);
  font-size: .76rem;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.forum-header-stats {
  margin-top: .4rem;
  color: var(--text-secondary);
  font-size: .74rem;
}

.forum-header-stats b {
  color: var(--tieba-blue);
  font-size: .84rem;
}

.feed-toolbar {
  display: block;
  padding: 1.15rem;
  border: 1px solid #e4e7ec;
  border-radius: 6px;
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

.toolbar {
  display: grid;
  grid-template-columns: minmax(240px, 1fr) 132px auto;
  gap: .6rem;
  min-width: 0;
}

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

.filter-trigger {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: .4rem;
  padding: .68rem .8rem;
  border: 1px solid #dfe2e8;
  border-radius: 4px;
  color: #4c5567;
  background: #fff;
  font: inherit;
  font-size: .82rem;
  font-weight: 650;
  cursor: pointer;
}

.filter-trigger:hover {
  color: var(--tieba-blue);
  border-color: #b9d5f3;
  background: #f4f9fe;
}

.filter-count {
  min-width: 1.2rem;
  padding: .08rem .3rem;
  border-radius: 999px;
  color: #fff;
  background: var(--tieba-blue);
  font-size: .66rem;
  text-align: center;
}

.icon-button {
  align-items: center;
  background: transparent;
  border: none;
  border-radius: 50%;
  color: #0f1419;
  cursor: pointer;
  display: inline-flex;
  font: inherit;
  font-size: 1.5rem;
  height: 36px;
  justify-content: center;
  line-height: 1;
  width: 36px;
}

.icon-button:hover {
  background: #eff3f4;
}

.filter-dialog-backdrop {
  position: fixed;
  inset: 0;
  z-index: 1200;
  display: grid;
  place-items: center;
  padding: 1rem;
  background: rgba(18, 24, 42, .58);
  backdrop-filter: blur(6px);
}

.filter-dialog {
  width: min(620px, 100%);
  max-height: calc(100vh - 2rem);
  overflow: auto;
  border: 1px solid rgba(255, 255, 255, .15);
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 28px 80px rgba(13, 22, 40, .3);
}

.filter-dialog-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  padding: 1.2rem 1.3rem;
  border-bottom: 1px solid var(--border);
}

.filter-dialog-header h3 {
  color: #171d2e;
  font-size: 1.15rem;
}

.filter-dialog-header p {
  margin-top: .2rem;
  color: var(--text-secondary);
  font-size: .76rem;
}

.filter-dialog-body {
  display: grid;
  gap: 1.25rem;
  padding: 1.25rem 1.3rem;
}

.tag-filter-field {
  min-width: 0;
  margin: 0;
  padding: 0;
  border: 0;
}

.tag-filter-field legend {
  margin-bottom: .35rem;
  color: var(--text-secondary);
  font-size: .8rem;
}

.tag-checkbox-list {
  display: flex;
  flex-wrap: wrap;
  gap: .5rem;
}

.tag-checkbox {
  display: inline-flex;
  align-items: center;
  gap: .4rem;
  padding: .5rem .65rem;
  border: 1px solid #e2e4ed;
  border-radius: 4px;
  color: #566074;
  background: #fff;
  cursor: pointer;
}

.tag-checkbox:has(input:checked) {
  color: var(--tieba-blue);
  border-color: #b9d5f3;
  background: var(--tieba-blue-light);
}

.tag-checkbox input[type="checkbox"] {
  width: 1rem;
  height: 1rem;
  margin: 0;
  padding: 0;
  accent-color: var(--tieba-blue);
  box-shadow: none;
}

.tag-checkbox small {
  color: #9299a8;
  font-size: .68rem;
}

.tag-filter-empty {
  margin: 0;
  color: var(--text-secondary);
  font-size: .78rem;
}

.filter-time-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: .75rem;
}

.filter-time-grid label {
  display: grid;
  gap: .4rem;
  color: var(--text-secondary);
  font-size: .8rem;
}

.filter-time-grid input {
  width: 100%;
  min-width: 0;
  padding: .65rem .7rem;
  border: 1px solid #dfe2e8;
  border-radius: 4px;
  background: #fff;
  font: inherit;
}

.filter-dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: .65rem;
  padding: 1rem 1.3rem;
  border-top: 1px solid var(--border);
}

.tieba-panel {
  margin-top: .9rem;
  border: 1px solid #e4e7ec;
  border-radius: 6px;
  background: #fff;
  overflow: hidden;
}

.sort-tabs {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  padding: 0 1rem;
  border-bottom: 1px solid #eceef2;
}

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

@media (max-width: 1000px) {
  .toolbar {
    flex-wrap: wrap;
  }

  .search-field input {
    flex-basis: 42%;
  }
}

@media (max-width: 820px) {
  .forum-layout {
    grid-template-columns: 1fr;
  }

  .forum-sidebar {
    position: static;
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .forum-sidebar-heading {
    grid-column: 1 / -1;
  }

  .feed-toolbar {
    position: static;
  }
}

@media (max-width: 640px) {
  .forum-sidebar {
    display: flex;
    gap: .4rem;
    overflow-x: auto;
    padding: .65rem;
    scrollbar-width: thin;
  }

  .forum-sidebar .section-title {
    display: none;
  }

  .forum-filter {
    flex: 0 0 auto;
    width: auto;
    gap: .5rem;
    margin: 0;
    white-space: nowrap;
  }

  .forum-filter-name {
    flex: 0 1 auto;
    max-width: 9rem;
  }

  .forum-header {
    padding: .9rem;
  }

  .forum-header-avatar {
    width: 46px;
    height: 46px;
    font-size: 1.15rem;
  }

  .feed-toolbar,
  .toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .toolbar {
    display: grid;
    grid-template-columns: 1fr auto;
  }

  .search-field {
    grid-column: 1 / -1;
  }

  .filter-time-grid {
    grid-template-columns: 1fr;
  }

  .filter-dialog-actions .btn {
    flex: 1;
  }
}
</style>
