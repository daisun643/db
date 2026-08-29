<template>
  <section class="post-detail-grid">
    <div class="detail-column">
      <section class="post-detail-panel">
        <div class="detail-header">
          <button class="icon-button" @click="goBack" aria-label="返回">
            <span>←</span>
          </button>
          <span v-if="selectedPost" class="muted">{{ selectedPost.forumName || '未分区' }}</span>
        </div>

        <div v-if="detailLoading" class="loading">加载中...</div>
        <template v-else-if="selectedPost">
          <article class="post-detail">
            <div class="post-meta">
              <span
                :class="['author-link', { plain: !selectedPost.userID }]"
                :title="selectedPost.userID ? '查看个人主页' : ''"
                @click="openUserHome(selectedPost.userID)"
              >{{ selectedPost.username || '匿名用户' }}</span>
              <span>{{ formatDate(selectedPost.createTime) }}</span>
              <span :class="['badge', selectedPost.status === 'Active' ? 'badge-green' : 'badge-yellow']">
                {{ selectedPost.status }}
              </span>
            </div>
            <h2>{{ selectedPost.title }}</h2>
            <div
              class="post-content markdown-body"
              v-html="renderMarkdown(selectedPost.content || selectedPost.contentPreview)"
            ></div>
            <div v-if="selectedPost.imageUrls?.length" class="detail-images">
              <img v-for="url in selectedPost.imageUrls" :key="url" :src="url" alt="" loading="lazy" />
            </div>
            <div class="post-actions">
              <span
                v-for="metric in postMetricItems(selectedPost)"
                :key="metric.key"
                class="post-metric"
                :title="metric.label"
                :aria-label="`${metric.label} ${metric.value}`"
              >
                <span class="post-action-svg" :style="iconMaskStyle(metric.icon)" aria-hidden="true"></span>
                <span>{{ metric.value }}</span>
              </span>
              <button
                :class="['post-icon-action', { liked: selectedPost.isLiked }]"
                @click="handleLike(selectedPost)"
                :title="selectedPost.isLiked ? '取消点赞' : '点赞'"
                :aria-label="selectedPost.isLiked ? '取消点赞' : '点赞'"
              >
                <span class="post-action-svg" :style="iconMaskStyle(heartIcon)" aria-hidden="true"></span>
              </button>
              <button
                :class="['post-icon-action', { favorited: selectedPost.isFavorited }]"
                @click.stop="handleFavorite(selectedPost)"
                :disabled="!selectedPost.isFavorited && favoriteFolders.length === 0"
                :title="selectedPost.isFavorited ? '取消收藏' : '收藏'"
                :aria-label="selectedPost.isFavorited ? '取消收藏' : '收藏'"
              >
                <span class="post-action-svg" :style="iconMaskStyle(bookmarkIcon)" aria-hidden="true"></span>
              </button>
              <button
                v-if="canEditPost(selectedPost)"
                class="post-icon-action"
                @click="openEditPost(selectedPost)"
                title="编辑"
                aria-label="编辑"
              >
                <span class="post-action-svg" :style="iconMaskStyle(editIcon)" aria-hidden="true"></span>
              </button>
              <button class="post-icon-action danger" @click="openReport(selectedPost)" title="举报" aria-label="举报">
                <span class="post-action-svg" :style="iconMaskStyle(flagIcon)" aria-hidden="true"></span>
              </button>
            </div>
          </article>

          <section class="comment-section">
            <div class="comment-head">
              <h3>评论 <span class="comment-count">{{ selectedPost.commentCount || 0 }}</span></h3>
            </div>

            <form class="composer" @submit.prevent="handleCreateComment(null)">
              <div class="composer-avatar" aria-hidden="true">{{ composerInitial }}</div>
              <div class="composer-main">
                <textarea
                  v-model="commentText"
                  placeholder="写下你的评论，支持 @用户名 提及..."
                  required
                  :disabled="!authStore.isAuthenticated"
                ></textarea>
                <div class="composer-bar">
                  <span class="composer-hint">{{ authStore.isAuthenticated ? '友善发言，理性讨论' : '登录后可发表评论' }}</span>
                  <button
                    class="composer-submit"
                    type="submit"
                    :disabled="commentSubmitting || !authStore.isAuthenticated"
                  >{{ commentSubmitting ? '发送中...' : '发表评论' }}</button>
                </div>
              </div>
            </form>

            <div v-if="commentsLoading" class="loading">加载评论中...</div>
            <div v-else-if="comments.length === 0" class="comment-empty">
              <span class="comment-empty-icon" aria-hidden="true">💬</span>
              <p>还没有评论，来抢沙发吧</p>
            </div>
            <div v-else class="comment-list">
              <div v-for="comment in comments" :key="comment.commentID" class="comment-item">
                <CommentNode
                  :comment="comment"
                  :author-id="selectedPost.userID"
                  :replying-to="replyingTo"
                  :reply-text="replyText"
                  @reply="startReply"
                  @cancel-reply="cancelReply"
                  @update-reply="replyText = $event.value"
                  @submit-reply="handleCreateComment"
                  @report="openCommentReport"
                  @delete="handleDeleteComment"
                />
              </div>
            </div>
          </section>
        </template>
        <div v-else class="empty-state compact">
          <p>帖子不存在或已被删除。</p>
        </div>
      </section>
    </div>

    <aside class="forum-rail">
      <section v-if="forumCard" class="rail-card forum-card">
        <div class="forum-card-header">
          <img v-if="canShowAvatar(forumCard.avatarUrl)" :src="forumCard.avatarUrl" :alt="forumCard.forumName" class="forum-card-avatar forum-card-avatar-img" @error="markAvatarFailed(forumCard.avatarUrl)" />
          <span v-else class="forum-card-avatar">{{ forumCardInitial }}</span>
          <div class="forum-card-title">
            <strong
              class="forum-card-name"
              role="link"
              tabindex="0"
              :title="'进入 ' + forumCard.forumName + ' 版块'"
              @click="goBoard(forumCard.forumID)"
              @keydown.enter="goBoard(forumCard.forumID)"
            >{{ forumCard.forumName }}</strong>
            <small>{{ forumCard.postCount || 0 }} 篇帖子 · {{ forumCard.memberCount || 0 }} 名成员</small>
          </div>
        </div>
        <p class="forum-card-desc">{{ forumCard.description || '这个版块还没有简介' }}</p>
        <button
          class="forum-join-btn"
          type="button"
          :class="{ joined: forumCard.isJoined }"
          :disabled="!authStore.isAuthenticated || togglingForumJoinId === forumCard.forumID"
          :title="!authStore.isAuthenticated ? '登录后可关注版块' : (forumCard.isJoined ? '点击取消关注' : '关注版块')"
          @click="handleToggleForumJoin(forumCard)"
        >{{ forumCard.isJoined ? '已关注' : '+ 关注版块' }}</button>

        <div class="forum-card-section">
          <span class="forum-card-label">版主</span>
          <template v-if="forumCard.managers?.length">
            <span
              v-for="manager in forumCard.managers"
              :key="manager.userID"
              class="forum-card-person"
              title="查看个人主页"
              @click="openUserHome(manager.userID)"
            >{{ manager.username || manager.email }}</span>
          </template>
          <span v-else class="forum-card-empty">暂无</span>
        </div>

        <div class="forum-card-section">
          <span class="forum-card-label">管理员</span>
          <span
            v-if="forumCard.creator"
            class="forum-card-person"
            title="查看个人主页"
            @click="openUserHome(forumCard.creator.userID)"
          >{{ forumCard.creator.username || forumCard.creator.email }}</span>
          <span v-else class="forum-card-empty">暂无</span>
        </div>
      </section>
    </aside>
  </section>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { useForum } from '../../composables/useForum'
import { getForum } from '../../api'
import { renderMarkdown } from '../../utils/markdown'
import { canShowAvatar, markAvatarFailed } from '../../utils/avatarFallback'
import CommentNode from '../../components/forum/CommentNode.vue'
import bookmarkIcon from '../../assets/icons/bookmark.svg'
import commentIcon from '../../assets/icons/comment.svg'
import editIcon from '../../assets/icons/edit.svg'
import eyeIcon from '../../assets/icons/eye.svg'
import flagIcon from '../../assets/icons/flag.svg'
import heartIcon from '../../assets/icons/heart.svg'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const {
  forums,
  selectedPost,
  detailLoading,
  comments,
  commentsLoading,
  favoriteFolders,
  commentText,
  commentSubmitting,
  replyingTo,
  replyText,
  loadPostDetail,
  closePostDetail,
  handleLike,
  handleFavorite,
  openEditPost,
  openReport,
  handleCreateComment,
  startReply,
  cancelReply,
  openCommentReport,
  handleDeleteComment,
  togglingForumJoinId,
  handleToggleForumJoin,
} = useForum()

const postId = computed(() => Number(route.params.postId))

// ---- 右边栏：版块信息 ----

const fallbackForum = ref(null)

const forumCard = computed(() => {
  if (!selectedPost.value?.forumID) return null
  return forums.value.find(f => f.forumID === selectedPost.value.forumID) || fallbackForum.value
})

const forumCardInitial = computed(() =>
  (forumCard.value?.forumName || '版')[0]?.toUpperCase() || '版'
)

const loadForumCard = async () => {
  fallbackForum.value = null
  const forumId = selectedPost.value?.forumID
  if (!forumId || forums.value.some(f => f.forumID === forumId)) return
  try {
    const res = await getForum(forumId)
    fallbackForum.value = res.data
  } catch (e) {
    fallbackForum.value = null
  }
}

const goBoard = (forumId) => {
  if (forumId) router.push(`/forums/board/${forumId}`)
}

const goBack = () => {
  if (window.history.length > 1) router.back()
  else router.push('/forums')
}

const openUserHome = (userId) => {
  if (userId) {
    router.push(`/user/${userId}`)
  }
}

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

const composerInitial = computed(() =>
  (authStore.user?.username || authStore.user?.email || '评')[0]?.toUpperCase() || '评'
)

const load = () => loadPostDetail(postId.value)

onMounted(load)
watch(postId, load)
watch(selectedPost, loadForumCard)
onBeforeUnmount(closePostDetail)
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

.post-detail-grid {
  align-items: start;
  display: grid;
  gap: 1rem;
  grid-template-columns: minmax(0, 1fr) 250px;
}

.detail-column {
  min-width: 0;
}

.forum-rail {
  display: grid;
  gap: 1rem;
  position: sticky;
  top: 1rem;
}

.rail-card {
  background: #fff;
  border: 1px solid #e4e8ef;
  border-radius: 14px;
  padding: 1rem;
}

.forum-card {
  display: grid;
  gap: .8rem;
}

.forum-card-header {
  align-items: center;
  display: flex;
  gap: .65rem;
}

.forum-card-avatar {
  background: linear-gradient(135deg, #4d9bf0, #2f7ee0);
  border-radius: 8px;
  color: #fff;
  display: grid;
  flex: 0 0 auto;
  font-size: 1.1rem;
  font-weight: 800;
  height: 44px;
  place-items: center;
  width: 44px;
}

.forum-card-avatar-img {
  object-fit: cover;
}

.forum-card-title {
  display: grid;
  gap: .15rem;
  min-width: 0;
}

.forum-card-name {
  color: #171d2e;
  cursor: pointer;
  font-size: .95rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.forum-card-name:hover {
  color: #2f7ee0;
}

.forum-card-title small {
  color: #9aa3b3;
  font-size: .68rem;
}

.forum-card-desc {
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 3;
  color: var(--text-secondary);
  display: -webkit-box;
  font-size: .74rem;
  line-height: 1.6;
  margin: 0;
  overflow: hidden;
}

.forum-join-btn {
  background: #2f7ee0;
  border: 0;
  border-radius: 999px;
  color: #fff;
  cursor: pointer;
  font: inherit;
  font-size: .78rem;
  font-weight: 750;
  padding: .5rem .9rem;
}

.forum-join-btn:hover:not(:disabled) {
  background: #266bc4;
}

.forum-join-btn.joined {
  background: #eef4ff;
  border: 1px solid #cfe2f7;
  color: #266bc4;
}

.forum-join-btn:disabled {
  cursor: not-allowed;
  opacity: .55;
}

.forum-card-section {
  border-top: 1px solid #f0f2f6;
  display: grid;
  gap: .3rem;
  padding-top: .65rem;
}

.forum-card-label {
  color: #9aa3b3;
  font-size: .66rem;
  font-weight: 800;
  letter-spacing: .08em;
}

.forum-card-person {
  color: #45536b;
  cursor: pointer;
  font-size: .76rem;
  font-weight: 650;
  width: fit-content;
}

.forum-card-person:hover {
  color: #2f7ee0;
  text-decoration: underline;
}

.forum-card-empty {
  color: #b6bcc7;
  font-size: .74rem;
}

.post-detail-panel {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 14px;
  margin: 0 auto;
  width: min(900px, 100%);
}

.detail-header {
  align-items: center;
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  border-bottom: 1px solid var(--border);
  padding: .8rem 1.1rem;
  background: #f8fbff;
  border-radius: 14px 14px 0 0;
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
  font-size: 1.25rem;
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
  padding: 1.5rem 2rem;
}

.post-detail h2 {
  color: #17233d;
  font-size: clamp(1.35rem, 2.5vw, 1.9rem);
  letter-spacing: -.035em;
  margin: .85rem 0 1rem;
}

.post-meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.post-meta .author-link {
  color: #5d4fd5;
  cursor: pointer;
  font-weight: 600;
}

.post-meta .author-link:hover {
  text-decoration: underline;
}

.post-meta .author-link.plain {
  color: inherit;
  cursor: default;
  font-weight: inherit;
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
  padding: 1.25rem 1.5rem 1.75rem;
}

.comment-head h3 {
  align-items: center;
  border-bottom: 1px solid var(--border);
  display: flex;
  font-size: 1.02rem;
  gap: .5rem;
  margin: 0 0 1.1rem;
  padding-bottom: .6rem;
}

.comment-count {
  background: #eef4ff;
  border-radius: 999px;
  color: #266bc4;
  font-size: .75rem;
  font-weight: 750;
  padding: .15rem .6rem;
}

.composer {
  background: #f8fafd;
  border: 1px solid #e4e8ef;
  border-radius: 14px;
  display: flex;
  gap: .7rem;
  margin-bottom: 1.35rem;
  padding: .85rem;
}

.composer-avatar {
  align-items: center;
  background: linear-gradient(135deg, #4d9bf0, #2f7ee0);
  border-radius: 50%;
  color: #fff;
  display: flex;
  flex: 0 0 auto;
  font-size: .9rem;
  font-weight: 700;
  height: 38px;
  justify-content: center;
  width: 38px;
}

.composer-main {
  display: grid;
  flex: 1;
  gap: .5rem;
  min-width: 0;
}

.composer textarea {
  background: #fff;
  border: 1px solid #dbe3ee;
  border-radius: 10px;
  font: inherit;
  font-size: .9rem;
  min-height: 76px;
  padding: .65rem .85rem;
  resize: vertical;
  transition: border-color .2s, box-shadow .2s;
}

.composer textarea:focus {
  border-color: #266bc4;
  box-shadow: 0 0 0 3px rgba(38, 107, 196, .12);
  outline: none;
}

.composer textarea:disabled {
  background: #f3f5f9;
  color: #9aa3b3;
}

.composer-bar {
  align-items: center;
  display: flex;
  justify-content: space-between;
  gap: .75rem;
}

.composer-hint {
  color: #9aa3b3;
  font-size: .74rem;
}

.composer-submit {
  background: #266bc4;
  border: none;
  border-radius: 999px;
  color: #fff;
  cursor: pointer;
  font: inherit;
  font-size: .82rem;
  font-weight: 700;
  padding: .5rem 1.2rem;
  transition: background .15s;
}

.composer-submit:hover:not(:disabled) {
  background: #1f5aa7;
}

.composer-submit:disabled {
  cursor: not-allowed;
  opacity: .5;
}

.comment-empty {
  border: 1px dashed #d9dbe5;
  border-radius: 14px;
  background: #fafaff;
  display: grid;
  gap: .35rem;
  justify-items: center;
  padding: 2.2rem 1rem;
  text-align: center;
}

.comment-empty-icon {
  font-size: 1.6rem;
  opacity: .55;
}

.comment-empty p {
  color: var(--text-secondary);
  font-size: .85rem;
  margin: 0;
}

.comment-list {
  display: flex;
  flex-direction: column;
}

.comment-item {
  border-bottom: 1px solid #eef1f5;
  padding: 1rem 0;
}

.comment-item:first-child {
  padding-top: .25rem;
}

.comment-item:last-child {
  border-bottom: none;
  padding-bottom: .25rem;
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

@media (max-width: 900px) {
  .post-detail-grid {
    display: block;
  }

  .forum-rail {
    margin-top: 1rem;
    position: static;
  }

  .post-detail-panel {
    border: none;
    border-radius: 0;
    width: 100%;
  }
}
</style>
