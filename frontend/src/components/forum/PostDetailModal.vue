<template>
  <div v-if="open" class="detail-backdrop" @click.self="$emit('close')">
    <section class="post-detail-panel">
      <div class="modal-header">
        <button class="icon-button" @click="$emit('close')" aria-label="关闭帖子详情">
          <span>×</span>
        </button>
        <span v-if="post" class="muted">{{ post.forumName || '未分区' }}</span>
      </div>

      <div v-if="detailLoading" class="loading">加载中...</div>
      <template v-else-if="post">
        <article class="post-detail">
          <div class="post-meta">
            <span>{{ post.username || '匿名用户' }}</span>
            <span>{{ formatDate(post.createTime) }}</span>
            <span :class="['badge', post.status === 'Active' ? 'badge-green' : 'badge-yellow']">
              {{ post.status }}
            </span>
          </div>
          <h2>{{ post.title }}</h2>
          <div
            class="post-content markdown-body"
            v-html="renderMarkdown(post.content || post.contentPreview)"
          ></div>
          <div v-if="post.imageUrls?.length" class="detail-images">
            <img v-for="url in post.imageUrls" :key="url" :src="url" alt="" loading="lazy" />
          </div>
          <div class="tag-row">
            <span v-for="tag in post.tags" :key="tag" class="tag">#{{ tag }}</span>
          </div>
          <div class="post-actions">
            <span
              v-for="metric in postMetricItems(post)"
              :key="metric.key"
              class="post-metric"
              :title="metric.label"
              :aria-label="`${metric.label} ${metric.value}`"
            >
              <span class="post-action-svg" :style="iconMaskStyle(metric.icon)" aria-hidden="true"></span>
              <span>{{ metric.value }}</span>
            </span>
            <button
              :class="['post-icon-action', { liked: post.isLiked }]"
              @click="$emit('like', post)"
              :title="post.isLiked ? '取消点赞' : '点赞'"
              :aria-label="post.isLiked ? '取消点赞' : '点赞'"
            >
              <span class="post-action-svg" :style="iconMaskStyle(heartIcon)" aria-hidden="true"></span>
            </button>
            <button
              :class="['post-icon-action', { favorited: post.isFavorited }]"
              @click.stop="$emit('favorite', post)"
              :disabled="!post.isFavorited && favoriteFolders.length === 0"
              :title="post.isFavorited ? '取消收藏' : '收藏'"
              :aria-label="post.isFavorited ? '取消收藏' : '收藏'"
            >
              <span class="post-action-svg" :style="iconMaskStyle(bookmarkIcon)" aria-hidden="true"></span>
            </button>
            <button
              v-if="canEditPost(post)"
              class="post-icon-action"
              @click="$emit('edit', post)"
              title="编辑"
              aria-label="编辑"
            >
              <span class="post-action-svg" :style="iconMaskStyle(editIcon)" aria-hidden="true"></span>
            </button>
            <button class="post-icon-action danger" @click="$emit('report', post)" title="举报" aria-label="举报">
              <span class="post-action-svg" :style="iconMaskStyle(flagIcon)" aria-hidden="true"></span>
            </button>
          </div>
        </article>

        <section class="comment-section">
          <h3>评论</h3>
          <form class="comment-form" @submit.prevent="$emit('submit-comment', null)">
            <textarea
              :value="commentText"
              @input="$emit('update:commentText', $event.target.value)"
              placeholder="写下评论，支持 @用户名 提及"
              required
            ></textarea>
            <button class="btn btn-primary" type="submit" :disabled="commentSubmitting">
              {{ commentSubmitting ? '发送中...' : '发表评论' }}
            </button>
          </form>

          <div v-if="commentsLoading" class="loading">加载评论中...</div>
          <div v-else class="comment-list">
            <div v-if="comments.length === 0" class="empty-state compact">
              <p>暂无评论</p>
            </div>
            <CommentNode
              v-for="comment in comments"
              :key="comment.commentID"
              :comment="comment"
              :replying-to="replyingTo"
              :reply-text="replyText"
              @reply="$emit('reply', $event)"
              @cancel-reply="$emit('cancel-reply')"
              @update-reply="$emit('update-reply', $event)"
              @submit-reply="$emit('submit-comment', $event)"
              @report="$emit('report-comment', $event)"
              @delete="$emit('delete-comment', $event)"
            />
          </div>
        </section>
      </template>
    </section>
  </div>
</template>

<script setup>
import { computed, defineComponent, h } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { renderMarkdown } from '../../utils/markdown'
import bookmarkIcon from '../../assets/icons/bookmark.svg'
import commentIcon from '../../assets/icons/comment.svg'
import editIcon from '../../assets/icons/edit.svg'
import eyeIcon from '../../assets/icons/eye.svg'
import flagIcon from '../../assets/icons/flag.svg'
import flameIcon from '../../assets/icons/flame.svg'
import heartIcon from '../../assets/icons/heart.svg'

const props = defineProps({
  open: { type: Boolean, required: true },
  post: { type: Object, default: null },
  detailLoading: { type: Boolean, default: false },
  comments: { type: Array, default: () => [] },
  commentsLoading: { type: Boolean, default: false },
  favoriteFolders: { type: Array, default: () => [] },
  commentText: { type: String, default: '' },
  commentSubmitting: { type: Boolean, default: false },
  replyingTo: { type: Number, default: null },
  replyText: { type: String, default: '' },
})

const emit = defineEmits([
  'close',
  'like',
  'favorite',
  'edit',
  'report',
  'submit-comment',
  'reply',
  'cancel-reply',
  'update-reply',
  'delete-comment',
  'report-comment',
  'update:commentText',
])

const authStore = useAuthStore()

const formatDate = (value) => {
  if (!value) return ''
  return new Date(value).toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

const postMetricItems = (post) => [
  { key: 'heat', label: '热度', value: post?.heatScore || 0, icon: flameIcon },
  { key: 'views', label: '浏览', value: post?.viewCount || 0, icon: eyeIcon },
  { key: 'likes', label: '点赞', value: post?.likeCount || 0, icon: heartIcon },
  { key: 'comments', label: '评论', value: post?.commentCount || 0, icon: commentIcon },
]

const iconMaskStyle = (icon) => ({
  '--icon-url': `url("${icon}")`,
})

const canEditPost = (post) => {
  return post?.userID && authStore.user?.userId && post.userID === authStore.user.userId
}

const CommentNode = defineComponent({
  name: 'CommentNode',
  props: {
    comment: { type: Object, required: true },
    replyingTo: { type: Number, default: null },
    replyText: { type: String, default: '' },
  },
  emits: ['reply', 'cancel-reply', 'update-reply', 'submit-reply', 'report', 'delete'],
  setup(props, { emit }) {
    const canDelete = () => {
      return props.comment.userID && authStore.user?.userId && props.comment.userID === authStore.user.userId
    }

    const initial = (name) => (name || '?')[0]?.toUpperCase() || '?'

    const isDeleted = props.comment.status === 'Deleted'

    const renderNode = () => h('article', { class: isDeleted ? 'comment-node deleted' : 'comment-node' }, [
      h('div', { class: isDeleted ? 'comment-avatar deleted' : 'comment-avatar' }, [
        h('span', isDeleted ? '' : initial(props.comment.username)),
      ]),
      h('div', { class: 'comment-body' }, [
        h('div', { class: 'comment-header' }, [
          h('span', { class: 'comment-author' }, isDeleted ? '用户已删除' : (props.comment.username || '用户')),
          h('span', { class: 'comment-time' }, formatDate(props.comment.createTime)),
        ]),
        h('div', { class: 'comment-content' }, isDeleted ? '用户已删除该评论' : (props.comment.content || '')),
        isDeleted ? null : h('div', { class: 'comment-actions' }, [
          h('span', { class: 'comment-action-link', onClick: () => emit('reply', props.comment) }, '回复'),
          h('span', { class: 'comment-action-link', onClick: () => emit('report', props.comment) }, '举报'),
          canDelete()
            ? h('span', { class: 'comment-action-link', onClick: () => emit('delete', props.comment) }, '删除')
            : null,
        ]),
        props.replyingTo === props.comment.commentID
          ? h('form', {
              class: 'reply-form',
              onSubmit: (event) => {
                event.preventDefault()
                emit('submit-reply', props.comment.commentID)
              },
            }, [
              h('textarea', {
                value: props.replyText,
                required: true,
                placeholder: '写下回复...',
                onInput: (event) => emit('update-reply', { commentId: props.comment.commentID, value: event.target.value }),
              }),
              h('div', { class: 'reply-actions' }, [
                h('button', { class: 'btn btn-primary btn-sm', type: 'submit' }, '发送'),
                h('button', { class: 'btn btn-sm', type: 'button', onClick: () => emit('cancel-reply') }, '取消'),
              ]),
            ])
          : null,
      ]),
      props.comment.replies?.length
        ? h('div', { class: 'comment-children' }, props.comment.replies.map(reply =>
            h(CommentNode, {
              key: reply.commentID,
              comment: reply,
              replyingTo: props.replyingTo,
              replyText: props.replyText,
              onReply: (comment) => emit('reply', comment),
              onCancelReply: () => emit('cancel-reply'),
              onUpdateReply: (payload) => emit('update-reply', payload),
              onSubmitReply: (id) => emit('submit-reply', id),
              onReport: (comment) => emit('report', comment),
              onDelete: (comment) => emit('delete', comment),
            })
          ))
        : null,
    ])

    return renderNode
  },
})
</script>

<style scoped>
.loading {
  padding: 1rem;
  text-align: center;
  color: var(--text-secondary);
}

.muted {
  color: var(--text-secondary);
}

.badge {
  display: inline-flex;
  padding: 0.125rem 0.5rem;
  border-radius: 9999px;
  font-size: 0.75rem;
}

.badge-green {
  background: #dcfce7;
  color: #166534;
}

.badge-yellow {
  background: #fef3c7;
  color: #92400e;
}

.detail-backdrop {
  align-items: flex-start;
  background: rgba(91, 112, 131, 0.4);
  bottom: 0;
  display: flex;
  justify-content: center;
  left: 0;
  padding: 3rem 1rem;
  position: fixed;
  right: 0;
  top: 0;
  z-index: 1200;
}

.post-detail-panel {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 16px;
  box-shadow: 0 20px 60px rgba(15, 23, 42, 0.24);
  max-height: calc(100vh - 6rem);
  max-width: 680px;
  overflow-y: auto;
  padding: 0;
  width: min(680px, 100vw);
}

.modal-header {
  align-items: center;
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  position: sticky;
  top: 0;
  z-index: 1;
  border-bottom: 1px solid var(--border);
  padding: 1rem;
  background: var(--surface);
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

.post-detail {
  border-bottom: 1px solid var(--border);
  padding: 1rem;
}

.post-detail h2 {
  font-size: 1.5rem;
  margin: 0.75rem 0;
}

.post-meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.post-content {
  color: var(--text);
  line-height: 1.7;
  white-space: pre-wrap;
}

.detail-images {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.75rem;
  margin: 0.75rem 0;
}

.detail-images img {
  aspect-ratio: 16 / 10;
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  object-fit: cover;
  width: 100%;
}

.tag-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.tag {
  color: #1d9bf0;
  background: #eff6ff;
  border-radius: 9999px;
  padding: 0.125rem 0.5rem;
  font-size: 0.75rem;
}

.post-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.75rem;
  max-width: 560px;
}

.post-metric,
.post-icon-action {
  align-items: center;
  border-radius: 9999px;
  color: #536471;
  display: inline-flex;
  gap: 0.375rem;
  min-height: 32px;
}

.post-metric {
  padding: 0.125rem 0.375rem;
}

.post-icon-action {
  background: transparent;
  border: none;
  cursor: pointer;
  justify-content: center;
  min-width: 32px;
  padding: 0.25rem;
  transition: background 0.15s, color 0.15s;
}

.post-action-svg {
  background: currentColor;
  display: block;
  height: 18px;
  mask: var(--icon-url) center / contain no-repeat;
  -webkit-mask: var(--icon-url) center / contain no-repeat;
  width: 18px;
}

.post-icon-action:hover,
.post-icon-action:focus-visible {
  background: rgba(29, 155, 240, 0.1);
  color: #1d9bf0;
  outline: none;
}

.post-icon-action.liked {
  color: #f91880;
}

.post-icon-action.liked:hover,
.post-icon-action.liked:focus-visible {
  background: rgba(249, 24, 128, 0.1);
}

.post-icon-action.favorited {
  color: #f59e0b;
}

.post-icon-action.favorited:hover,
.post-icon-action.favorited:focus-visible {
  background: rgba(245, 158, 11, 0.1);
}

.post-icon-action.danger:hover,
.post-icon-action.danger:focus-visible {
  background: rgba(244, 33, 46, 0.1);
  color: #f4212e;
}

.post-icon-action:disabled {
  cursor: not-allowed;
  opacity: 0.45;
}

.comment-section {
  padding: 1rem;
}

.comment-section h3 {
  font-size: 1rem;
  margin-bottom: 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--border);
}

.comment-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 1.25rem;
}

.comment-form textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  min-height: 88px;
  padding: 0.75rem 1rem;
  resize: vertical;
  transition: border-color 0.2s;
}

.comment-form textarea:focus {
  border-color: var(--primary);
  outline: none;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.5rem 1rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
  color: var(--text);
  font: inherit;
  cursor: pointer;
}

.btn-primary {
  background: var(--primary);
  border-color: var(--primary);
  color: #fff;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-sm {
  padding: 0.375rem 0.75rem;
  font-size: 0.8125rem;
}

.empty-state {
  border: 1px dashed #d9dbe5;
  border-radius: 20px;
  background: #fafaff;
  padding: 1rem;
  text-align: center;
}

.empty-state p {
  margin: 0;
  color: var(--text-secondary);
}

.comment-list {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.comment-node {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  padding: 1rem 0;
  border-bottom: 1px solid var(--border);
}

.comment-node:last-child {
  border-bottom: none;
}

.comment-avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: linear-gradient(135deg, var(--primary), #6366f1);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.875rem;
  font-weight: 600;
  flex-shrink: 0;
}

.comment-body {
  flex: 1;
  min-width: 0;
}

.comment-header {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  margin-bottom: 0.375rem;
}

.comment-author {
  font-weight: 400;
  font-size: 0.8125rem;
  color: #536471;
}

.comment-time {
  font-size: 0.75rem;
  color: #536471;
}

.comment-node.deleted > .comment-body > .comment-header > .comment-author,
.comment-node.deleted > .comment-body > .comment-header > .comment-time {
  color: #b9c1c9;
}

.comment-node.deleted > .comment-body > .comment-content {
  color: #b9c1c9;
  font-style: italic;
}

.comment-avatar.deleted {
  background: #b9c1c9;
  color: white;
}

.comment-content {
  color: #0f1419;
  line-height: 1.65;
  font-size: 0.9375rem;
  white-space: pre-wrap;
  word-break: break-word;
}

.comment-actions {
  display: flex;
  gap: 1rem;
  margin-top: 0.5rem;
}

.comment-action-link {
  color: #536471;
  cursor: pointer;
  font-size: 0.8125rem;
}

.comment-action-link:hover {
  color: #0f1419;
  text-decoration: underline;
}

.comment-children {
  width: 100%;
  margin-left: calc(36px + 0.75rem);
  padding-left: 1rem;
  border-left: 2px solid var(--border);
  display: flex;
  flex-direction: column;
}

.comment-children .comment-node {
  padding: 0.75rem 0;
}

.comment-children .comment-avatar {
  width: 28px;
  height: 28px;
  font-size: 0.75rem;
}

.reply-form {
  margin-top: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.reply-form textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  font-size: 0.875rem;
  min-height: 64px;
  padding: 0.5rem 0.75rem;
  resize: vertical;
  transition: border-color 0.2s;
}

.reply-form textarea:focus {
  border-color: var(--primary);
  outline: none;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.reply-actions {
  display: flex;
  gap: 0.5rem;
}

@media (max-width: 900px) {
  .detail-backdrop {
    display: block;
    padding: 0;
  }

  .post-detail-panel {
    border: none;
    border-radius: 0;
    max-height: 100vh;
    width: 100vw;
  }

  .modal-header {
    align-items: flex-start;
  }
}
</style>
