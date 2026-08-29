<template>
  <article
    class="tieba-row"
    role="button"
    tabindex="0"
    @click="$emit('open', post)"
    @keydown.enter="$emit('open', post)"
  >
    

    <div class="row-main">
      <div v-if="showForum && post.forumName" class="row-forum-line">
        <span
          class="row-forum"
          role="link"
          tabindex="0"
          :title="'进入 ' + post.forumName + ' 版块'"
          @click.stop="openForumBoard"
          @keydown.enter.stop="openForumBoard"
        >
          <img v-if="forumAvatarUrl" :src="forumAvatarUrl" :alt="post.forumName" class="row-forum-avatar" @error="markAvatarFailed(cardForum?.avatarUrl)" />
          <span v-else class="row-forum-avatar row-forum-avatar-fallback">{{ forumAvatarInitial }}</span>
          # {{ post.forumName }}
        </span>
      </div>
      <div class="row-title-line">
        <span v-if="post.status === 'Pinned'" class="status-tag pinned">置顶</span>
        <span v-if="post.status === 'Elite'" class="status-tag elite">精</span>
        <h3 class="row-title">{{ post.title }}</h3>
      </div>

      <div v-if="thumbnails.length" class="row-thumbs">
        <img
          v-for="url in thumbnails"
          :key="url"
          :src="url"
          :alt="post.title"
          loading="lazy"
          @click.stop="$emit('open', post)"
        />
      </div>

      <p v-if="post.contentPreview" class="row-excerpt">{{ post.contentPreview }}</p>

      <div class="row-meta">
        <div
          class="row-author"
          :class="{ clickable: !!post.userID }"
          role="link"
          tabindex="0"
          title="查看个人主页"
          @click.stop="openAuthorHome"
          @keydown.enter.stop="openAuthorHome"
        >
          <span class="author-avatar">{{ authorInitial }}</span>
          <span class="author-name">{{ post.username || '校园用户' }}</span>
        </div>
        <div class="row-actions">
          <template v-if="mode === 'mine'">
            <button type="button" @click.stop="$emit('edit', post)">编辑</button>
            <button type="button" class="danger" @click.stop="$emit('delete', post)">删除</button>
          </template>
          <template v-else-if="mode === 'favorite'">
            <button
              :class="{ active: post.isLiked }"
              type="button"
              @click.stop="$emit('like', post)"
            >
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8l1.1 1.1L12 21l7.8-7.5 1.1-1.1a5.5 5.5 0 0 0-.1-7.8Z" /></svg>
              {{ post.likeCount || 0 }}
            </button>
            <button type="button" class="danger" @click.stop="$emit('remove-favorite', post)">移出收藏</button>
          </template>
          <template v-else>
            <button
              :class="{ active: post.isLiked }"
              type="button"
              :aria-label="post.isLiked ? '取消点赞' : '点赞'"
              @click.stop="$emit('like', post)"
            >
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8l1.1 1.1L12 21l7.8-7.5 1.1-1.1a5.5 5.5 0 0 0-.1-7.8Z" /></svg>
              {{ post.likeCount || 0 }}
            </button>
            <button
              :class="{ active: post.isFavorited }"
              type="button"
              @click.stop="$emit('favorite', post)"
            >
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 3h12a1 1 0 0 1 1 1v17l-7-4-7 4V4a1 1 0 0 1 1-1Z" /></svg>
              {{ post.isFavorited ? '已收藏' : '收藏' }}
            </button>
            <button type="button" @click.stop="$emit('report', post)">举报</button>
          </template>
        </div>
        <span class="row-date">{{ formattedDate }}</span>
      </div>
    </div>
  </article>
</template>

<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useForum } from '../../composables/useForum'
import { canShowAvatar, markAvatarFailed } from '../../utils/avatarFallback'

const props = defineProps({
  post: { type: Object, required: true },
  mode: { type: String, default: 'feed' },
  showForum: { type: Boolean, default: false },
})

defineEmits(['open', 'like', 'favorite', 'report', 'edit', 'delete', 'remove-favorite'])

const router = useRouter()
const { forums } = useForum()

const cardForum = computed(() =>
  props.post.forumID ? forums.value.find(f => f.forumID === props.post.forumID) || null : null
)

const forumAvatarUrl = computed(() => {
  const url = cardForum.value?.avatarUrl || ''
  return canShowAvatar(url) ? url : ''
})

const forumAvatarInitial = computed(() =>
  (props.post.forumName || '版')[0]?.toUpperCase() || '版'
)

const openAuthorHome = () => {
  if (props.post.userID) {
    router.push(`/user/${props.post.userID}`)
  }
}

const openForumBoard = () => {
  if (props.post.forumID) {
    router.push(`/forums/board/${props.post.forumID}`)
  }
}

const thumbnails = computed(() => (props.post.imageUrls || []).slice(0, 3))
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
.tieba-row {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  padding: .9rem 1rem;
  border-bottom: 1px solid #eceef2;
  background: #fff;
  cursor: pointer;
  transition: background .15s ease;
}

.tieba-row:first-child {
  border-radius: 6px 6px 0 0;
}

.tieba-row:last-child {
  border-bottom: 0;
  border-radius: 0 0 6px 6px;
}

.tieba-row:hover,
.tieba-row:focus-visible {
  background: #f6f8fb;
  outline: none;
}

.row-stats {
  flex: 0 0 52px;
  display: grid;
  gap: .15rem;
  padding-top: .1rem;
  text-align: center;
}

.stat-replies {
  color: #2f7ee0;
  font-size: 1.02rem;
  font-weight: 750;
  line-height: 1.2;
}

.stat-views {
  color: #9aa2b0;
  font-size: .7rem;
}

.row-main {
  flex: 1;
  min-width: 0;
}

.row-title-line {
  display: flex;
  align-items: center;
  gap: .45rem;
}

.status-tag {
  flex: 0 0 auto;
  padding: .1rem .35rem;
  border-radius: 3px;
  font-size: .64rem;
  font-weight: 700;
  line-height: 1.4;
}

.status-tag.pinned {
  color: #2f7ee0;
  background: #e6f1fc;
}

.status-tag.elite {
  color: #fff;
  background: #f25d5d;
}

.row-title {
  margin: 0;
  overflow: hidden;
  color: #24405e;
  font-size: .95rem;
  font-weight: 600;
  line-height: 1.5;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tieba-row:hover .row-title {
  color: #2f7ee0;
}

.row-thumbs {
  display: flex;
  gap: .5rem;
  margin-top: .55rem;
}

.row-thumbs img {
  width: 96px;
  height: 72px;
  object-fit: cover;
  border-radius: 4px;
  background: #f2f3f5;
}

.row-meta {
  display: flex;
  align-items: center;
  gap: .9rem;
  margin-top: .55rem;
  color: #9aa2b0;
  font-size: .72rem;
}

.row-excerpt { display: -webkit-box; overflow: hidden; margin: .45rem 0 0; color: #7c8798; font-size: .76rem; line-height: 1.55; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }

.row-author {
  display: inline-flex;
  align-items: center;
  gap: .35rem;
  min-width: 0;
}

.row-author.clickable {
  cursor: pointer;
}

.row-author.clickable:hover .author-name {
  color: #2f7ee0;
}

.author-avatar {
  width: 22px;
  height: 22px;
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  border-radius: 4px;
  color: #fff;
  background: #2f7ee0;
  font-size: .66rem;
  font-weight: 750;
}

.author-name {
  overflow: hidden;
  max-width: 9rem;
  color: #5f6b7c;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.row-forum-line {
  display: flex;
  align-items: center;
  gap: .5rem;
  margin-bottom: .35rem;
}

.row-forum {
  display: inline-flex;
  align-items: center;
  gap: .35rem;
  max-width: 14rem;
  overflow: hidden;
  padding: .12rem .5rem;
  border-radius: 3px;
  background: #f2f5f9;
  color: #7c8698;
  font-size: .68rem;
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
  cursor: pointer;
}

.row-forum-avatar {
  width: 16px;
  height: 16px;
  flex: 0 0 auto;
  border-radius: 3px;
  object-fit: cover;
}

.row-forum-avatar-fallback {
  display: grid;
  place-items: center;
  color: #fff;
  background: linear-gradient(135deg, #4d9bf0, #2f7ee0);
  font-size: .58rem;
  font-weight: 750;
}

.row-forum:hover {
  background: #e6f1fc;
  color: #2f7ee0;
}

.row-date {
  margin-left: auto;
}

.row-actions {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: .8rem;
}

.row-actions button {
  display: inline-flex;
  align-items: center;
  gap: .28rem;
  border: 0;
  padding: .2rem 0;
  color: #9aa2b0;
  background: transparent;
  font: inherit;
  font-size: .72rem;
  cursor: pointer;
}

.row-actions button:hover {
  color: #2f7ee0;
}

.row-actions button.active {
  color: #f25d5d;
}

.row-actions button.danger:hover {
  color: #d92626;
}

.row-actions svg {
  width: 14px;
  height: 14px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.8;
}

.row-actions button.active svg {
  fill: currentColor;
}

@media (max-width: 640px) {
  .tieba-row {
    gap: .65rem;
    padding: .75rem .7rem;
  }

  .row-stats {
    flex-basis: 40px;
  }

  .row-thumbs img {
    width: 76px;
    height: 58px;
  }

  .row-forum {
    display: none;
  }

  .row-actions {
    display: none;
  }
}
</style>
