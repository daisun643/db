<template>
  <article
    class="forum-post-card"
    role="button"
    tabindex="0"
    @click="$emit('open', post)"
    @keydown.enter="$emit('open', post)"
  >
    <div :class="['post-card-cover', { 'text-cover': !coverImage }]">
      <img v-if="coverImage" :src="coverImage" :alt="post.title" loading="lazy" />
      <div v-else class="text-cover-content">
        <span>{{ post.forumName || '校园论坛' }}</span>
        <strong>{{ post.title }}</strong>
      </div>
      <span class="forum-pill">{{ post.forumName || '未分区' }}</span>
      <span v-if="post.imageUrls?.length > 1" class="image-count">{{ post.imageUrls.length }} 图</span>
    </div>

    <div class="post-card-body">
      <h3>{{ post.title }}</h3>
      <div v-if="post.tags?.length" class="card-tags">
        <span v-for="tag in post.tags.slice(0, 3)" :key="tag">#{{ tag }}</span>
      </div>

      <footer class="post-card-footer">
        <div
          class="card-author"
          :class="{ clickable: !!post.userID }"
          role="link"
          tabindex="0"
          title="查看个人主页"
          @click.stop="openAuthorHome"
          @keydown.enter.stop="openAuthorHome"
        >
          <span class="author-avatar">{{ authorInitial }}</span>
          <span class="author-copy">
            <strong>{{ post.username || '校园用户' }}</strong>
            <small>{{ formattedDate }}</small>
          </span>
        </div>
        <div class="card-engagement">
          <button
            :class="['card-icon-button', { active: post.isLiked }]"
            type="button"
            :aria-label="post.isLiked ? '取消点赞' : '点赞'"
            @click.stop="$emit('like', post)"
          >
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8l1.1 1.1L12 21l7.8-7.5 1.1-1.1a5.5 5.5 0 0 0-.1-7.8Z" /></svg>
            <span>{{ post.likeCount || 0 }}</span>
          </button>
          <span class="card-stat" title="评论">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M21 15a4 4 0 0 1-4 4H8l-5 3V7a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4Z" /></svg>
            {{ post.commentCount || 0 }}
          </span>
        </div>
      </footer>

      <div v-if="mode !== 'feed'" class="card-management">
        <template v-if="mode === 'mine'">
          <button type="button" @click.stop="$emit('edit', post)">编辑</button>
          <button type="button" class="danger" @click.stop="$emit('delete', post)">删除</button>
        </template>
        <button v-else type="button" class="danger" @click.stop="$emit('remove-favorite', post)">移出收藏夹</button>
      </div>

      <div v-else class="card-quick-actions">
        <button
          :class="{ active: post.isFavorited }"
          type="button"
          :aria-label="post.isFavorited ? '取消收藏' : '收藏'"
          @click.stop="$emit('favorite', post)"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 3h12a1 1 0 0 1 1 1v17l-7-4-7 4V4a1 1 0 0 1 1-1Z" /></svg>
          {{ post.isFavorited ? '已收藏' : '收藏' }}
        </button>
        <button type="button" @click.stop="$emit('report', post)">举报</button>
      </div>
    </div>
  </article>
</template>

<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'

const props = defineProps({
  post: { type: Object, required: true },
  mode: { type: String, default: 'feed' },
})

defineEmits(['open', 'like', 'favorite', 'report', 'edit', 'delete', 'remove-favorite'])

const router = useRouter()

const openAuthorHome = () => {
  if (props.post.userID) {
    router.push(`/user/${props.post.userID}`)
  }
}

const coverImage = computed(() => props.post.imageUrls?.[0] || '')
const authorInitial = computed(() => (props.post.username || '校')[0]?.toUpperCase() || '校')
const formattedDate = computed(() => {
  if (!props.post.createTime) return '刚刚'
  return new Date(props.post.createTime).toLocaleDateString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
  })
})
</script>

<style scoped>
.forum-post-card {
  display: inline-block;
  width: 100%;
  margin: 0 0 1rem;
  overflow: hidden;
  break-inside: avoid;
  border: 1px solid rgba(28, 31, 44, 0.08);
  border-radius: 18px;
  background: #fff;
  box-shadow: 0 5px 18px rgba(25, 28, 42, 0.045);
  cursor: pointer;
  transition: transform .2s ease, box-shadow .2s ease, border-color .2s ease;
}

.forum-post-card:hover,
.forum-post-card:focus-visible {
  transform: translateY(-3px);
  border-color: rgba(255, 64, 84, 0.24);
  outline: none;
  box-shadow: 0 14px 34px rgba(35, 30, 52, 0.1);
}

.post-card-cover {
  position: relative;
  min-height: 180px;
  overflow: hidden;
  background: #f2f2f5;
}

.post-card-cover img {
  display: block;
  width: 100%;
  min-height: 180px;
  max-height: 360px;
  object-fit: cover;
  transition: transform .35s ease;
}

.forum-post-card:hover .post-card-cover img { transform: scale(1.025); }

.text-cover {
  min-height: 210px;
  display: grid;
  place-items: center;
  padding: 1.5rem;
  background:
    radial-gradient(circle at 84% 12%, rgba(255,255,255,.4), transparent 30%),
    linear-gradient(145deg, #ffe9ec, #f7dde9 48%, #e9e2ff);
}

.text-cover-content { display: grid; gap: .8rem; text-align: center; }
.text-cover-content span { color: #9b6072; font-size: .7rem; font-weight: 750; letter-spacing: .12em; }
.text-cover-content strong { color: #392c40; font-size: 1.15rem; line-height: 1.45; }

.forum-pill,
.image-count {
  position: absolute;
  top: .75rem;
  padding: .34rem .55rem;
  border-radius: 999px;
  color: #fff;
  background: rgba(24, 20, 31, .58);
  backdrop-filter: blur(10px);
  font-size: .65rem;
  font-weight: 700;
}
.forum-pill { left: .75rem; }
.image-count { right: .75rem; }

.post-card-body { padding: .85rem .9rem .75rem; }
.post-card-body h3 { margin: 0; color: #18181b; font-size: .95rem; line-height: 1.45; letter-spacing: -.015em; }
.card-tags { display: flex; flex-wrap: wrap; gap: .35rem; margin-top: .65rem; }
.card-tags span { color: #6e5ec6; font-size: .68rem; }

.post-card-footer { display: flex; align-items: center; justify-content: space-between; gap: .5rem; margin-top: .8rem; }
.card-author { min-width: 0; display: flex; align-items: center; gap: .5rem; }
.card-author.clickable { cursor: pointer; }
.card-author.clickable:hover .author-copy strong { color: #6e5ec6; text-decoration: underline; }
.author-avatar { width: 28px; height: 28px; display: grid; place-items: center; flex: 0 0 auto; border-radius: 50%; color: #fff; background: linear-gradient(135deg, #ff6678, #8b6be8); font-size: .7rem; font-weight: 800; }
.author-copy { min-width: 0; display: flex; flex-direction: column; }
.author-copy strong { overflow: hidden; color: #4c4c52; font-size: .7rem; font-weight: 650; text-overflow: ellipsis; white-space: nowrap; }
.author-copy small { margin-top: .08rem; color: #aaaab1; font-size: .6rem; }

.card-engagement { display: flex; align-items: center; gap: .35rem; color: #8b8b92; }
.card-icon-button,
.card-stat { min-height: 28px; display: inline-flex; align-items: center; gap: .25rem; border: 0; color: inherit; background: transparent; font: inherit; font-size: .68rem; }
.card-icon-button { padding: .25rem; cursor: pointer; }
.card-icon-button.active { color: #ff4054; }
.card-icon-button svg,
.card-stat svg,
.card-quick-actions svg { width: 15px; height: 15px; fill: none; stroke: currentColor; stroke-width: 1.8; }
.card-icon-button.active svg { fill: currentColor; }

.card-management,
.card-quick-actions { display: flex; align-items: center; gap: .5rem; margin-top: .7rem; padding-top: .65rem; border-top: 1px solid #f0f0f2; }
.card-management button,
.card-quick-actions button { display: inline-flex; align-items: center; gap: .3rem; border: 0; color: #777780; background: transparent; font: inherit; font-size: .68rem; cursor: pointer; }
.card-management button:hover,
.card-quick-actions button:hover,
.card-quick-actions button.active { color: #ff4054; }
.card-management .danger { margin-left: auto; color: #b76068; }
.card-quick-actions button:last-child { margin-left: auto; }

@media (max-width: 640px) {
  .forum-post-card { margin-bottom: .75rem; border-radius: 14px; }
  .post-card-cover,
  .post-card-cover img { min-height: 150px; }
  .text-cover { min-height: 180px; padding: 1rem; }
  .post-card-body { padding: .72rem; }
}
</style>
