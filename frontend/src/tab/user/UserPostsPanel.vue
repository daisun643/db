<template>
  <div class="tab-content user-posts-panel">
    <div v-if="loading" class="loading">加载中...</div>
    <template v-else>
      <div v-if="posts.length === 0" class="empty-state">
        <p>暂无发布的帖子</p>
      </div>
      <div v-else class="post-list">
        <article v-for="post in posts" :key="post.postID" class="post-row">
          <div class="post-row-main">
            <div class="post-row-title">
              <h3>{{ post.title }}</h3>
              <span :class="['badge', post.status === 'Active' ? 'badge-green' : 'badge-yellow']">
                {{ statusText(post.status) }}
              </span>
            </div>
            <p class="post-preview">{{ post.contentPreview || '暂无内容' }}</p>
            <div class="post-meta">
              <span>{{ post.forumName || '未分区' }}</span>
              <span v-for="tag in (post.tags || []).slice(0, 3)" :key="tag" class="post-tag">#{{ tag }}</span>
              <span>{{ formatDate(post.createTime) }}</span>
            </div>
          </div>
          <div class="post-row-stats">
            <span title="点赞">{{ post.likeCount || 0 }} 赞</span>
            <span title="评论">{{ post.commentCount || 0 }} 评论</span>
            <span title="浏览">{{ post.viewCount || 0 }} 浏览</span>
          </div>
        </article>
      </div>
      <div v-if="totalPages > 1" class="panel-pagination">
        <button class="btn" :disabled="page <= 1" @click="changePage(page - 1)">上一页</button>
        <span class="muted">第 {{ page }} / {{ totalPages }} 页 · 共 {{ totalPosts }} 条</span>
        <button class="btn" :disabled="page >= totalPages" @click="changePage(page + 1)">下一页</button>
      </div>
    </template>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { getPosts } from '../../api'

const props = defineProps({
  userId: { type: Number, required: true },
})

const emit = defineEmits(['error'])

const posts = ref([])
const loading = ref(false)
const totalPosts = ref(0)
const page = ref(1)
const pageSize = 10
const totalPages = computed(() => Math.max(1, Math.ceil(totalPosts.value / pageSize)))

const statusText = (status) => ({
  Active: '正常',
  Elite: '精华',
  Pinned: '置顶',
}[status] || status || '未知')

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  })
}

const loadPosts = async () => {
  try {
    loading.value = true
    const res = await getPosts({ authorId: props.userId, page: page.value, pageSize })
    posts.value = Array.isArray(res.data) ? res.data : []
    const totalFromHeader = Number(res.headers?.['x-total-count'])
    totalPosts.value = Number.isFinite(totalFromHeader) ? totalFromHeader : posts.value.length
  } catch (e) {
    posts.value = []
    totalPosts.value = 0
    emit('error', '无法加载 TA 的帖子: ' + (e.response?.data?.message || e.message))
  } finally {
    loading.value = false
  }
}

const changePage = (next) => {
  page.value = next
  loadPosts()
}

watch(() => props.userId, () => {
  page.value = 1
  loadPosts()
})

onMounted(loadPosts)

defineExpose({ reload: loadPosts })
</script>

<style scoped>
.user-posts-panel { display: grid; gap: 1rem; }
.post-list { display: grid; gap: .8rem; }
.post-row { display: flex; justify-content: space-between; gap: 1rem; padding: 1rem 1.1rem; border: 1px solid var(--border); border-radius: 14px; background: var(--surface); }
.post-row-main { min-width: 0; flex: 1; }
.post-row-title { display: flex; align-items: center; gap: .6rem; }
.post-row-title h3 { margin: 0; font-size: .98rem; color: #1d2438; }
.post-preview { margin: .45rem 0 0; color: var(--text-secondary); font-size: .82rem; line-height: 1.5; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.post-meta { display: flex; flex-wrap: wrap; gap: .6rem; margin-top: .55rem; color: #8a90a1; font-size: .72rem; }
.post-tag { color: #6e5ec6; }
.post-row-stats { display: flex; flex-direction: column; justify-content: center; gap: .3rem; flex: 0 0 auto; color: #8a90a1; font-size: .74rem; white-space: nowrap; }
.panel-pagination { display: flex; align-items: center; justify-content: center; gap: .9rem; }
.muted { color: var(--text-secondary); font-size: .8rem; }
.empty-state { padding: 2.5rem 0; text-align: center; color: var(--text-secondary); }
@media (max-width: 640px) {
  .post-row { flex-direction: column; }
  .post-row-stats { flex-direction: row; gap: .9rem; }
}
</style>
