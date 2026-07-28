<template>
  <div class="page-container forum-page">
    <section class="forum-hero">
      <div class="forum-hero-copy">
        <span class="forum-eyebrow"><i></i>COMMUNITY FORUM</span>
        <h1>让校园里的每个声音，<span>都被看见。</span></h1>
        <p>分享经验、发起讨论、寻找同好。这里收集真实而鲜活的校园日常，也欢迎你的下一条动态。</p>
        <button class="forum-hero-action" type="button" @click="openComposer">
          <span>＋</span> 发布新帖子
        </button>
      </div>
      <div class="forum-pulse-card">
        <span class="pulse-label">COMMUNITY PULSE</span>
        <strong>社区正在发生</strong>
        <div class="pulse-grid">
          <span><b>{{ totalPosts }}</b><small>当前帖子</small></span>
          <span><b>{{ forums.length }}</b><small>活跃版块</small></span>
          <span><b>{{ tagStats.length }}</b><small>热门标签</small></span>
        </div>
        <div class="pulse-live"><i></i> 校园社区实时开放</div>
      </div>
    </section>

    <div class="forum-nav">
      <div class="tabs">
        <button 
          :class="['tab', { active: activeTab === 'all' }]" 
          @click="activeTab = 'all'"
        >
          所有论坛
        </button>
        <button 
          :class="['tab', { active: activeTab === 'my-posts' }]" 
          @click="activeTab = 'my-posts'"
        >
          我的帖子
        </button>
        <button 
          :class="['tab', { active: activeTab === 'favorites' }]" 
          @click="activeTab = 'favorites'"
        >
          收藏夹
        </button>
      </div>
      <span class="forum-nav-note">分享 · 连接 · 共鸣</span>
    </div>

    <div v-if="error" class="error-message">{{ error }}</div>
    <div v-if="notice" class="success-message">{{ notice }}</div>

    <div v-if="activeTab === 'all'" class="forum-layout">
      <aside class="forum-sidebar">
        <div class="section-title">版块</div>
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
          <span>{{ forum.forumName }}</span>
          <span class="forum-count">{{ forum.postCount || 0 }}</span>
        </button>

        <div class="section-title" style="margin-top:1.5rem">热门标签</div>
        <div v-if="tagStats.length === 0" class="muted" style="font-size:0.8rem;padding:0 0.5rem">暂无标签</div>
        <button
          v-for="stat in tagStats"
          :key="stat.tagId"
          :class="['forum-filter', { active: filters.tag === stat.tagName }]"
          @click="selectTag(stat.tagName)"
        >
          <span>#{{ stat.tagName }}</span>
          <span class="forum-count">{{ stat.postCount }}</span>
        </button>
      </aside>

      <main class="forum-main">
        <div class="feed-toolbar">
          <div class="toolbar">
            <input v-model="filters.keyword" type="search" placeholder="搜索标题或内容" @keyup.enter="applyFilters" />
            <input v-model="filters.tag" type="search" placeholder="单个标签" @keyup.enter="applyFilters" />
            <select v-model="filters.sort" @change="applyFilters">
              <option value="latest">最新</option>
              <option value="hot">热度</option>
            </select>
            <button class="btn btn-primary" @click="applyFilters">筛选</button>
          </div>
          <button class="compose-trigger" @click="openComposer">发布帖子</button>
        </div>

        <details class="advanced-search">
          <summary>高级筛选：多个标签、时间与热度</summary>
          <div class="advanced-search-grid">
            <label>
              多个标签（逗号分隔）
              <input v-model="filters.tags" type="text" placeholder="如：数据库, 课程设计" @keyup.enter="applyFilters" />
            </label>
            <label>
              标签关系
              <select v-model="filters.tagOp">
                <option value="and">同时包含</option>
                <option value="or">包含任一</option>
              </select>
            </label>
            <label>
              开始时间
              <input v-model="filters.from" type="datetime-local" />
            </label>
            <label>
              结束时间
              <input v-model="filters.to" type="datetime-local" />
            </label>
            <label>
              最低热度
              <input v-model.number="filters.minHeat" type="number" min="0" placeholder="不限" />
            </label>
            <label>
              最高热度
              <input v-model.number="filters.maxHeat" type="number" min="0" placeholder="不限" />
            </label>
          </div>
          <div class="advanced-search-actions">
            <button class="btn btn-primary" @click="applyFilters">应用高级筛选</button>
            <button class="btn" @click="resetFilters">清空筛选</button>
          </div>
        </details>

        <div v-if="loading" class="loading">加载中...</div>
        <div v-else class="post-list">
          <article
            v-for="post in posts"
            :key="post.postID"
            class="post-item"
            role="button"
            tabindex="0"
            @click="openPostDetail(post)"
            @keydown.enter="openPostDetail(post)"
          >
            <div class="post-meta">
              <span>{{ post.forumName || '未分区' }}</span>
              <span>{{ post.username || '匿名用户' }}</span>
              <span>{{ formatDate(post.createTime) }}</span>
              <span :class="['badge', post.status === 'Active' ? 'badge-green' : 'badge-yellow']">
                {{ post.status }}
              </span>
            </div>
            <button class="post-title-button" @click.stop="openPostDetail(post)">
              {{ post.title }}
            </button>
            <p>{{ post.contentPreview }}</p>
            <div v-if="post.imageUrls?.length" class="image-strip">
              <img v-for="url in post.imageUrls.slice(0, 3)" :key="url" :src="url" alt="" loading="lazy" />
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
                @click.stop="handleLike(post)"
                :title="post.isLiked ? '取消点赞' : '点赞'"
                :aria-label="post.isLiked ? '取消点赞' : '点赞'"
              >
                <span class="post-action-svg" :style="iconMaskStyle(heartIcon)" aria-hidden="true"></span>
              </button>
              <button class="post-icon-action" @click.stop="openPostDetail(post)" title="查看详情" aria-label="查看详情">
                <span class="post-action-svg" :style="iconMaskStyle(openIcon)" aria-hidden="true"></span>
              </button>
              <button
                :class="['post-icon-action', { favorited: post.isFavorited }]"
                @click.stop="handleFavorite(post)"
                :disabled="!post.isFavorited && favoriteFolders.length === 0"
                :title="post.isFavorited ? '取消收藏' : '收藏'"
                :aria-label="post.isFavorited ? '取消收藏' : '收藏'"
              >
                <span class="post-action-svg" :style="iconMaskStyle(bookmarkIcon)" aria-hidden="true"></span>
              </button>
              <button class="post-icon-action danger" @click.stop="openReport(post)" title="举报" aria-label="举报">
                <span class="post-action-svg" :style="iconMaskStyle(flagIcon)" aria-hidden="true"></span>
              </button>
            </div>
          </article>
          <div v-if="posts.length === 0" class="empty-state">
            <p>暂无帖子</p>
          </div>
          <div v-if="totalPosts > 0" class="pagination-row">
            <button class="btn" :disabled="filters.page <= 1" @click="loadPosts(filters.page - 1)">上一页</button>
            <span>第 {{ filters.page }} 页 · 共 {{ totalPosts }} 条</span>
            <button class="btn" :disabled="!hasNextPage" @click="loadPosts(filters.page + 1)">下一页</button>
          </div>
        </div>
      </main>
    </div>

    <div v-else-if="activeTab === 'my-posts'" class="tab-content">
      <div v-if="loadingMyPosts" class="loading">加载中...</div>
      <div v-else class="post-list">
        <article
          v-for="post in myPosts"
          :key="post.postID"
          class="post-item"
          role="button"
          tabindex="0"
          @click="openPostDetail(post)"
          @keydown.enter="openPostDetail(post)"
        >
          <div class="post-meta">
            <span>{{ post.forumName || '未分区' }}</span>
            <span>{{ formatDate(post.createTime) }}</span>
            <span :class="['badge', post.status === 'Active' ? 'badge-green' : 'badge-yellow']">
              {{ post.status }}
            </span>
          </div>
          <button class="post-title-button" @click.stop="openPostDetail(post)">
            {{ post.title }}
          </button>
          <p>{{ post.contentPreview }}</p>
          <div class="post-actions">
            <button class="link-button" @click.stop="openEditPost(post)">编辑</button>
            <button class="link-button danger" @click.stop="handleDeletePost(post)">删除</button>
          </div>
        </article>
        <div v-if="myPosts.length === 0" class="empty-state">
          <p>暂无帖子</p>
        </div>
      </div>
    </div>

    <div v-else-if="activeTab === 'favorites'" class="favorites-layout">
      <aside class="favorites-sidebar">
        <div class="section-title">收藏夹</div>
        <form @submit.prevent="handleCreateFolder" class="folder-create-form">
          <input v-model="folderName" type="text" placeholder="新建收藏夹名称" required />
          <button class="btn" type="submit">创建</button>
        </form>
        <div class="folder-list">
          <div
            v-for="folder in favoriteFolders"
            :key="folder.folderID"
            :class="['folder-item', { active: selectedFolderId === folder.folderID }]"
            @click="selectFolder(folder.folderID)"
          >
            <div class="folder-item-row">
              <template v-if="renamingFolderId === folder.folderID">
                <input
                  v-model="renameText"
                  type="text"
                  class="folder-rename-input"
                  required
                  @keyup.enter="handleRenameFolderInline(folder.folderID)"
                  @keyup.escape="cancelRename"
                  @click.stop
                  ref="renameInput"
                />
              </template>
              <template v-else>
                <span class="folder-name">{{ folder.folderName }}</span>
                <span class="folder-count">{{ folder.postCount || 0 }}</span>
              </template>
            </div>
            <div class="folder-item-actions">
              <span class="folder-action-link" @click.stop="startRename(folder)">重命名</span>
              <span class="folder-action-link danger" @click.stop="handleDeleteFolderById(folder.folderID)">删除</span>
            </div>
          </div>
          <div v-if="favoriteFolders.length === 0" class="folder-list-empty">
            暂无收藏夹
          </div>
        </div>
      </aside>

      <main class="favorites-main">
        <div v-if="!selectedFolderId" class="empty-state">
          <p>选择一个收藏夹查看帖子</p>
        </div>
        <div v-else-if="loadingFavorites" class="loading">加载中...</div>
        <div v-else class="post-list">
          <article
            v-for="post in favoritePosts"
            :key="post.postID"
            class="post-item"
            role="button"
            tabindex="0"
            @click="openPostDetail(post)"
            @keydown.enter="openPostDetail(post)"
          >
            <div class="post-meta">
              <span>{{ post.forumName || '未分区' }}</span>
              <span>{{ post.username || '匿名用户' }}</span>
            </div>
            <button class="post-title-button" @click.stop="openPostDetail(post)">
              {{ post.title }}
            </button>
            <p>{{ post.contentPreview }}</p>
            <button class="link-button danger" @click.stop="handleRemoveFavorite(post)">取消收藏</button>
          </article>
          <div v-if="favoritePosts.length === 0" class="empty-state">
            <p>暂无收藏帖子</p>
          </div>
        </div>
      </main>
    </div>

    <div v-if="composerOpen" class="detail-backdrop" @click.self="closeComposer">
      <form class="post-detail-panel composer-modal" @submit.prevent="handleCreatePost">
        <div class="modal-header">
          <button class="icon-button" type="button" @click="closeComposer" aria-label="关闭发布窗口">
            <span>×</span>
          </button>
          <button class="compose-submit" type="submit" :disabled="submitting">
            {{ submitting ? '发布中...' : '发布' }}
          </button>
        </div>
        <div class="composer-shell">
          <div class="composer-avatar">{{ userInitial }}</div>
          <div class="composer-fields">
            <select v-model.number="postForm.forumID" required>
              <option disabled value="">选择版块</option>
              <option v-for="forum in forums" :key="forum.forumID" :value="forum.forumID">
                {{ forum.forumName }}
              </option>
            </select>
            <input v-model="postForm.title" type="text" placeholder="帖子标题" required />
            <textarea v-model="postForm.content" placeholder="有什么新鲜事？" required autofocus></textarea>
            <div class="composer-row">
              <input v-model="tagText" type="text" placeholder="标签，用逗号分隔" />
              <input
                type="file"
                multiple
                accept="image/jpeg,image/png,image/gif,image/webp"
                :disabled="submitting"
                @change="handlePickCreateImages"
              />
            </div>
            <div v-if="createImagePreviewUrls.length" class="image-strip">
              <div v-for="(url, index) in createImagePreviewUrls" :key="url" class="image-preview-item">
                <img :src="url" :alt="`预览图 ${index + 1}`" loading="lazy" />
                <button type="button" class="image-remove" @click="removeCreateImage(index)">移除</button>
              </div>
            </div>
            <p class="field-hint">最多可上传 6 张，单张不超过 5MB，仅支持 JPG/PNG/GIF/WebP。</p>
            <div class="composer-tools">
              <button class="link-button" type="button" @click="handleSuggestTags">推荐标签</button>
            </div>
          </div>
        </div>
      </form>
    </div>

    <div v-if="detailOpen" class="detail-backdrop" @click.self="closePostDetail">
      <section class="post-detail-panel">
        <div class="modal-header">
          <button class="icon-button" @click="closePostDetail" aria-label="关闭帖子详情">
            <span>×</span>
          </button>
          <span v-if="selectedPost" class="muted">{{ selectedPost.forumName || '未分区' }}</span>
        </div>

        <div v-if="detailLoading" class="loading">加载中...</div>
        <template v-else-if="selectedPost">
          <article class="post-detail">
            <div class="post-meta">
              <span>{{ selectedPost.username || '匿名用户' }}</span>
              <span>{{ formatDate(selectedPost.createTime) }}</span>
              <span :class="['badge', selectedPost.status === 'Active' ? 'badge-green' : 'badge-yellow']">
                {{ selectedPost.status }}
              </span>
            </div>
            <h2>{{ selectedPost.title }}</h2>
            <p class="post-content">{{ selectedPost.content || selectedPost.contentPreview }}</p>
            <div v-if="selectedPost.imageUrls?.length" class="detail-images">
              <img v-for="url in selectedPost.imageUrls" :key="url" :src="url" alt="" loading="lazy" />
            </div>
            <div class="tag-row">
              <span v-for="tag in selectedPost.tags" :key="tag" class="tag">#{{ tag }}</span>
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
            <h3>评论</h3>
            <form class="comment-form" @submit.prevent="handleCreateComment(null)">
              <textarea v-model="commentText" placeholder="写下评论，支持 @用户名 提及" required></textarea>
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
                @reply="startReply"
                @cancel-reply="cancelReply"
                @update-reply="replyText = $event"
                @submit-reply="handleCreateComment"
                @report="openCommentReport"
                @delete="handleDeleteComment"
              />
            </div>
          </section>
        </template>
      </section>
    </div>

    <div v-if="editingPost" class="detail-backdrop" @click.self="closeEditPost">
      <form class="post-detail-panel composer-modal" @submit.prevent="handleUpdatePost">
        <div class="modal-header">
          <button class="icon-button" type="button" @click="closeEditPost" aria-label="关闭编辑窗口">
            <span>×</span>
          </button>
          <button class="compose-submit" type="submit" :disabled="editingSaving">
            {{ editingSaving ? '保存中...' : '保存' }}
          </button>
        </div>
        <div class="composer-shell">
          <div class="composer-avatar">{{ userInitial }}</div>
          <div class="composer-fields">
            <input v-model="editForm.title" type="text" placeholder="帖子标题" required />
            <textarea v-model="editForm.content" placeholder="帖子内容" required></textarea>
            <div class="composer-row">
              <input v-model="editTagText" type="text" placeholder="标签，用逗号分隔" />
              <input
                type="file"
                multiple
                accept="image/jpeg,image/png,image/gif,image/webp"
                :disabled="editingSaving"
                @change="handlePickEditImages"
              />
            </div>
            <div v-if="combinedEditImageList.length" class="image-strip">
              <div v-for="(item, index) in combinedEditImageList" :key="`${item.source}-${index}`" class="image-preview-item">
                <img :src="item.url" :alt="`图片 ${index + 1}`" loading="lazy" />
                <button type="button" class="image-remove" @click="removeEditImage(index)">移除</button>
              </div>
            </div>
            <p v-if="combinedEditImageList.length" class="field-hint">最多 6 张，编辑时新老图片会按列表顺序提交。</p>
          </div>
        </div>
      </form>
    </div>

    <div v-if="reportTarget" class="detail-backdrop" @click.self="reportTarget = null">
      <form class="post-detail-panel report-form" @submit.prevent="handleCreateReport">
        <div class="detail-header">
          <button class="link-button" type="button" @click="reportTarget = null">取消举报</button>
          <span class="muted">{{ reportTarget.typeLabel }} #{{ reportTarget.id }}</span>
        </div>
        <h2>举报{{ reportTarget.typeLabel }}：{{ reportTarget.title }}</h2>
        <textarea v-model="reportReason" placeholder="描述违规原因" required></textarea>
        <button class="btn btn-primary" type="submit">提交举报</button>
      </form>
    </div>

    <div v-if="folderPickerOpen" class="detail-backdrop" @click.self="closeFolderPicker">
      <div class="post-detail-panel folder-picker-panel" role="dialog" aria-label="选择收藏夹">
        <div class="modal-header">
          <button class="icon-button" @click="closeFolderPicker" aria-label="关闭收藏夹选择">
            <span>×</span>
          </button>
          <span class="muted">选择收藏夹</span>
        </div>
        <ul class="folder-pick-list">
          <li
            v-for="folder in favoriteFolders"
            :key="folder.folderID"
            class="folder-pick-item"
            role="button"
            tabindex="0"
            @click="selectFolderForFavorite(folder.folderID)"
            @keydown.enter="selectFolderForFavorite(folder.folderID)"
          >
            <span>{{ folder.folderName }}</span>
            <span class="folder-post-count">{{ folder.postCount || 0 }}</span>
          </li>
          <li v-if="favoriteFolders.length === 0" class="folder-pick-empty">
            暂无收藏夹，请先创建一个
          </li>
        </ul>
        <form @submit.prevent="handlePickerCreateFolder" class="folder-picker-form">
          <input v-model="pickerFolderName" type="text" placeholder="新收藏夹名称" required />
          <button class="btn" type="submit">创建并收藏</button>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, defineComponent, h, onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
import bookmarkIcon from '../assets/icons/bookmark.svg'
import commentIcon from '../assets/icons/comment.svg'
import editIcon from '../assets/icons/edit.svg'
import eyeIcon from '../assets/icons/eye.svg'
import flagIcon from '../assets/icons/flag.svg'
import flameIcon from '../assets/icons/flame.svg'
import heartIcon from '../assets/icons/heart.svg'
import openIcon from '../assets/icons/open.svg'
import {
  addPostToFavoriteFolder,
  createComment,
  createFavoriteFolder,
  createPost,
  createReport,
  deleteFavoriteFolder,
  deleteComment,
  deletePost,
  getFavoriteFolderPosts,
  getFavoriteFolders,
  getForums,
  getMyPosts,
  getPost,
  getPostComments,
  getPosts,
  getTagStats,
  likePost,
  removePostFromFavoriteFolder,
  suggestTags,
  uploadImages,
  unfavoritePost,
  unlikePost,
  updateFavoriteFolder,
  updatePost,
} from '../api'

const authStore = useAuthStore()
const activeTab = ref('all')
const forums = ref([])
const tagStats = ref([])
const posts = ref([])
const myPosts = ref([])
const favoriteFolders = ref([])
const favoritePosts = ref([])
const loading = ref(true)
const loadingMyPosts = ref(false)
const loadingFavorites = ref(false)
const detailLoading = ref(false)
const commentsLoading = ref(false)
const submitting = ref(false)
const commentSubmitting = ref(false)
const composerOpen = ref(false)
const error = ref(null)
const notice = ref('')
const tagText = ref('')
const folderName = ref('')
const folderRenameName = ref('')
const selectedFolderId = ref('')
const renamingFolderId = ref(null)
const renameText = ref('')
const detailOpen = ref(false)
const selectedPost = ref(null)
const comments = ref([])
const commentText = ref('')
const replyingTo = ref(null)
const replyText = ref('')
const editingPost = ref(null)
const editingSaving = ref(false)
const editTagText = ref('')
const editImageUrls = ref([])
const editImageNewUrls = ref([])
const editImageFiles = ref([])
const createImageFiles = ref([])
const createImagePreviewUrls = ref([])
const reportTarget = ref(null)
const reportReason = ref('')
const folderPickerOpen = ref(false)
const folderPickerTarget = ref(null)
const pickerFolderName = ref('')
const userInitial = computed(() => (authStore.user?.username || '用')[0]?.toUpperCase() || '用')
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/gif', 'image/webp']
const MAX_IMAGE_BYTES = 5 * 1024 * 1024
const MAX_IMAGE_COUNT = 6

const postMetricItems = (post) => [
  { key: 'heat', label: '热度', value: post?.heatScore || 0, icon: flameIcon },
  { key: 'views', label: '浏览', value: post?.viewCount || 0, icon: eyeIcon },
  { key: 'likes', label: '点赞', value: post?.likeCount || 0, icon: heartIcon },
  { key: 'comments', label: '评论', value: post?.commentCount || 0, icon: commentIcon },
]

const combinedEditImageList = computed(() => {
  const fromRemote = editImageUrls.value.map((url, index) => ({
    source: 'remote',
    key: `remote-${index}`,
    url,
  }))
  const fromLocal = editImageNewUrls.value.map((url, index) => ({
    source: 'local',
    key: `local-${index}`,
    url,
  }))

  return [...fromRemote, ...fromLocal]
})

const iconMaskStyle = (icon) => ({
  '--icon-url': `url("${icon}")`,
})

const filters = ref({
  forumId: null,
  keyword: '',
  tag: '',
  tags: '',
  tagOp: 'and',
  from: '',
  to: '',
  minHeat: null,
  maxHeat: null,
  sort: 'latest',
  page: 1,
  pageSize: 20,
})
const totalPosts = ref(0)
const hasNextPage = ref(false)

const postForm = ref({
  forumID: '',
  title: '',
  content: '',
})

const editForm = ref({
  title: '',
  content: '',
})

const loadForums = async () => {
  const res = await getForums()
  forums.value = res.data
  if (!postForm.value.forumID && forums.value.length > 0) {
    postForm.value.forumID = forums.value[0].forumID
  }
}

const loadPosts = async (page = filters.value.page) => {
  try {
    loading.value = true
    error.value = null
    filters.value.page = Math.max(1, Number(page) || 1)
    const res = await getPosts({
      forumId: filters.value.forumId || undefined,
      keyword: filters.value.keyword.trim() || undefined,
      tag: filters.value.tag.trim() || undefined,
      tags: filters.value.tags.trim() || undefined,
      tagOp: filters.value.tagOp,
      from: filters.value.from || undefined,
      to: filters.value.to || undefined,
      minHeat: Number.isFinite(filters.value.minHeat) ? filters.value.minHeat : undefined,
      maxHeat: Number.isFinite(filters.value.maxHeat) ? filters.value.maxHeat : undefined,
      sort: filters.value.sort,
      page: filters.value.page,
      pageSize: filters.value.pageSize,
    })
    posts.value = Array.isArray(res.data) ? res.data : []
    const totalFromHeader = Number(res.headers?.['x-total-count'])
    totalPosts.value = Number.isFinite(totalFromHeader) ? totalFromHeader : posts.value.length
    hasNextPage.value = filters.value.page * filters.value.pageSize < totalPosts.value
  } catch (e) {
    posts.value = []
    totalPosts.value = 0
    hasNextPage.value = false
    error.value = '无法加载帖子数据: ' + (e.response?.data?.message || e.message)
  } finally {
    loading.value = false
  }
}

const loadMyPosts = async () => {
  try {
    loadingMyPosts.value = true
    const res = await getMyPosts()
    myPosts.value = res.data
  } catch (e) {
    error.value = '无法加载我的帖子: ' + (e.response?.data?.message || e.message)
  } finally {
    loadingMyPosts.value = false
  }
}

const loadFavoriteFolders = async () => {
  const res = await getFavoriteFolders()
  favoriteFolders.value = res.data
}

const loadFavoritePosts = async () => {
  if (!selectedFolderId.value) {
    favoritePosts.value = []
    return
  }

  try {
    loadingFavorites.value = true
    const res = await getFavoriteFolderPosts(selectedFolderId.value)
    favoritePosts.value = res.data
  } catch (e) {
    error.value = '无法加载收藏: ' + (e.response?.data?.message || e.message)
  } finally {
    loadingFavorites.value = false
  }
}

const selectForum = async (forumId) => {
  filters.value.forumId = forumId
  filters.value.page = 1
  await loadPosts()
}

const loadTagStats = async () => {
  try {
    const res = await getTagStats()
    tagStats.value = res.data
  } catch (e) {
    // 静默跳过
  }
}

const selectTag = async (tagName) => {
  if (filters.value.tag === tagName) {
    filters.value.tag = ''
  } else {
    filters.value.tag = tagName
  }
  filters.value.page = 1
  await loadPosts()
}

const applyFilters = async () => {
  filters.value.page = 1
  await loadPosts()
}

const resetFilters = async () => {
  filters.value = {
    forumId: filters.value.forumId,
    keyword: '',
    tag: '',
    tags: '',
    tagOp: 'and',
    from: '',
    to: '',
    minHeat: null,
    maxHeat: null,
    sort: 'latest',
    page: 1,
    pageSize: 20,
  }
  await loadPosts()
}

const clearCreateImageState = () => {
  createImagePreviewUrls.value.forEach(url => URL.revokeObjectURL(url))
  createImageFiles.value = []
  createImagePreviewUrls.value = []
}

const clearEditImageState = () => {
  editImageNewUrls.value.forEach(url => URL.revokeObjectURL(url))
  editImageFiles.value = []
  editImageNewUrls.value = []
  editImageUrls.value = []
}

const validateImageFile = (file) => {
  if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
    return '仅支持 JPG、PNG、GIF、WebP 图片'
  }
  if (file.size > MAX_IMAGE_BYTES) {
    return '单张图片不能超过5MB'
  }
  return null
}

const handlePickCreateImages = async (event) => {
  const files = Array.from(event.target.files || [])
  event.target.value = ''
  if (files.length === 0) return

  const nextCount = createImageFiles.value.length + files.length
  if (nextCount > MAX_IMAGE_COUNT) {
    error.value = `图片数量不能超过 ${MAX_IMAGE_COUNT} 张`
    return
  }

  for (const file of files) {
    const message = validateImageFile(file)
    if (message) {
      error.value = message
      return
    }
  }

  createImageFiles.value = [...createImageFiles.value, ...files]
  const addedUrls = files.map(file => URL.createObjectURL(file))
  createImagePreviewUrls.value = [...createImagePreviewUrls.value, ...addedUrls]
  error.value = null
}

const removeCreateImage = (index) => {
  const removedUrl = createImagePreviewUrls.value[index]
  if (removedUrl) {
    URL.revokeObjectURL(removedUrl)
  }
  createImagePreviewUrls.value.splice(index, 1)
  createImageFiles.value.splice(index, 1)
}

const handlePickEditImages = (event) => {
  const files = Array.from(event.target.files || [])
  event.target.value = ''
  if (files.length === 0) return

  const nextCount = editImageUrls.value.length + editImageNewUrls.value.length + files.length
  if (nextCount > MAX_IMAGE_COUNT) {
    error.value = `图片数量不能超过 ${MAX_IMAGE_COUNT} 张`
    return
  }

  for (const file of files) {
    const message = validateImageFile(file)
    if (message) {
      error.value = message
      return
    }
  }

  const addedUrls = files.map(file => URL.createObjectURL(file))
  editImageFiles.value = [...editImageFiles.value, ...files]
  editImageNewUrls.value = [...editImageNewUrls.value, ...addedUrls]
  error.value = null
}

const removeEditImage = (index) => {
  const remoteCount = editImageUrls.value.length
  if (index < remoteCount) {
    editImageUrls.value = editImageUrls.value.filter((_, i) => i !== index)
    return
  }

  const localIndex = index - remoteCount
  const removedUrl = editImageNewUrls.value[localIndex]
  if (removedUrl) {
    URL.revokeObjectURL(removedUrl)
  }
  editImageNewUrls.value.splice(localIndex, 1)
  editImageFiles.value.splice(localIndex, 1)
}

const openComposer = () => {
  error.value = null
  composerOpen.value = true
}

const closeComposer = () => {
  if (submitting.value) return
  clearCreateImageState()
  composerOpen.value = false
}

const handleCreatePost = async () => {
  try {
    submitting.value = true
    error.value = null
    const tagNames = tagText.value.split(/[,，]/).map(tag => tag.trim()).filter(Boolean)
    const uploaded = createImageFiles.value.length > 0
      ? await uploadImages(createImageFiles.value, 'posts')
      : null
    const imageUrls = (uploaded?.data?.urls || []).slice(0, MAX_IMAGE_COUNT)

    const res = await createPost({
      ...postForm.value,
      tagNames,
      imageUrls,
    })
    notice.value = res.data?.status === 'PendingReview'
      ? '帖子已提交审核：内容命中敏感词，暂不会公开展示；可在“我的帖子”查看审核状态。'
      : '帖子发布成功。'
    postForm.value = {
      ...postForm.value,
      title: '',
      content: '',
    }
    tagText.value = ''
    clearCreateImageState()
    composerOpen.value = false
    await Promise.all([loadPosts(), loadMyPosts()])
  } catch (e) {
    error.value = '发布失败: ' + (e.response?.data?.message || e.message)
  } finally {
    submitting.value = false
  }
}

const handleSuggestTags = async () => {
  const res = await suggestTags({
    title: postForm.value.title,
    content: postForm.value.content,
  })
  tagText.value = res.data.join(', ')
}

const handleLike = async (post) => {
  const res = post.isLiked ? await unlikePost(post.postID) : await likePost(post.postID)
  const nextLiked = res.data.liked
  const nextCount = res.data.likeCount
  updatePostLikeState(post.postID, nextLiked, nextCount)
}

const updatePostLikeState = (postId, isLiked, likeCount) => {
  for (const collection of [posts.value, myPosts.value, favoritePosts.value]) {
    const target = collection.find(item => item.postID === postId)
    if (target) {
      target.isLiked = isLiked
      target.likeCount = likeCount
    }
  }

  if (selectedPost.value?.postID === postId) {
    selectedPost.value.isLiked = isLiked
    selectedPost.value.likeCount = likeCount
  }
}

const updatePostFavoriteState = (postId, isFavorited) => {
  for (const collection of [posts.value, myPosts.value, favoritePosts.value]) {
    const target = collection.find(item => item.postID === postId)
    if (target) {
      target.isFavorited = isFavorited
    }
  }

  if (selectedPost.value?.postID === postId) {
    selectedPost.value.isFavorited = isFavorited
  }
}

const handleFavorite = async (post) => {
  if (post.isFavorited) {
    await unfavoritePost(post.postID)
    updatePostFavoriteState(post.postID, false)
    await loadFavoriteFolders()
    if (activeTab.value === 'favorites') await loadFavoritePosts()
    return
  }

  if (favoriteFolders.value.length === 0) {
    await createFavoriteFolder({ folderName: '默认收藏夹' })
    await loadFavoriteFolders()
  }
  folderPickerTarget.value = post
  folderPickerOpen.value = true
}

const selectFolderForFavorite = async (folderId) => {
  if (!folderPickerTarget.value) return
  await addPostToFavoriteFolder(folderId, folderPickerTarget.value.postID)
  updatePostFavoriteState(folderPickerTarget.value.postID, true)
  await loadFavoriteFolders()
  closeFolderPicker()
}

const handlePickerCreateFolder = async () => {
  if (!pickerFolderName.value.trim()) return
  const res = await createFavoriteFolder({ folderName: pickerFolderName.value.trim() })
  pickerFolderName.value = ''
  await loadFavoriteFolders()
  if (folderPickerTarget.value) {
    await addPostToFavoriteFolder(res.data.folderID, folderPickerTarget.value.postID)
    updatePostFavoriteState(folderPickerTarget.value.postID, true)
    await loadFavoriteFolders()
  }
  closeFolderPicker()
}

const closeFolderPicker = () => {
  folderPickerOpen.value = false
  folderPickerTarget.value = null
  pickerFolderName.value = ''
}

const handleDeletePost = async (post) => {
  await deletePost(post.postID)
  await Promise.all([loadPosts(), loadMyPosts()])
}

const canEditPost = (post) => {
  return post?.userID && authStore.user?.userId && post.userID === authStore.user.userId
}

const openEditPost = async (post) => {
  try {
    error.value = null
    const res = await getPost(post.postID)
    editingPost.value = res.data
    clearEditImageState()
    editForm.value = {
      title: res.data.title || '',
      content: res.data.content || '',
    }
    editTagText.value = (res.data.tags || []).join(', ')
    editImageUrls.value = [...(res.data.imageUrls || [])]
  } catch (e) {
    error.value = '无法加载编辑内容: ' + (e.response?.data?.message || e.message)
  }
}

const closeEditPost = () => {
  editingPost.value = null
  editTagText.value = ''
  clearEditImageState()
}

const handleUpdatePost = async () => {
  if (!editingPost.value) return

  try {
    editingSaving.value = true
    error.value = null
    const tagNames = editTagText.value.split(/[,，]/).map(tag => tag.trim()).filter(Boolean)
    const uploaded = editImageFiles.value.length > 0
      ? await uploadImages(editImageFiles.value, 'posts')
      : null
    const imageUrls = [
      ...editImageUrls.value,
      ...(uploaded?.data?.urls || []),
    ].slice(0, MAX_IMAGE_COUNT)
    const res = await updatePost(editingPost.value.postID, {
      ...editForm.value,
      tagNames,
      imageUrls,
    })
    if (selectedPost.value?.postID === editingPost.value.postID) {
      selectedPost.value = res.data
    }
    closeEditPost()
    await Promise.all([loadPosts(), loadMyPosts()])
  } catch (e) {
    error.value = '保存失败: ' + (e.response?.data?.message || e.message)
  } finally {
    editingSaving.value = false
  }
}

const openReport = (post) => {
  reportTarget.value = {
    targetType: 'Post',
    typeLabel: '帖子',
    id: post.postID,
    title: post.title,
  }
  reportReason.value = ''
}

const openCommentReport = (comment) => {
  reportTarget.value = {
    targetType: 'Comment',
    typeLabel: '评论',
    id: comment.commentID,
    title: comment.content || '评论内容',
  }
  reportReason.value = ''
}

const handleCreateReport = async () => {
  if (!reportTarget.value) return

  try {
    error.value = null
    await createReport({
      targetType: reportTarget.value.targetType,
      targetID: reportTarget.value.id,
      reason: reportReason.value,
    })
    reportTarget.value = null
    reportReason.value = ''
  } catch (e) {
    error.value = '举报失败: ' + (e.response?.data?.message || e.message)
  }
}

const handleDeleteComment = async (comment) => {
  if (!selectedPost.value) return

  try {
    error.value = null
    await deleteComment(comment.commentID)
    await loadComments()
    const refreshed = await getPost(selectedPost.value.postID)
    selectedPost.value = refreshed.data
  } catch (e) {
    error.value = '删除评论失败: ' + (e.response?.data?.message || e.message)
  }
}

const handleCreateFolder = async () => {
  const res = await createFavoriteFolder({ folderName: folderName.value })
  folderName.value = ''
  await loadFavoriteFolders()
  selectedFolderId.value = res.data.folderID
  await loadFavoritePosts()
}

const selectFolder = async (folderId) => {
  selectedFolderId.value = folderId
  await loadFavoritePosts()
}

const startRename = (folder) => {
  renamingFolderId.value = folder.folderID
  renameText.value = folder.folderName
}

const cancelRename = () => {
  renamingFolderId.value = null
  renameText.value = ''
}

const handleRenameFolderInline = async (folderId) => {
  if (!renameText.value.trim()) return
  await updateFavoriteFolder(folderId, { folderName: renameText.value.trim() })
  renamingFolderId.value = null
  renameText.value = ''
  await loadFavoriteFolders()
}

const handleDeleteFolderById = async (folderId) => {
  await deleteFavoriteFolder(folderId)
  if (selectedFolderId.value === folderId) {
    selectedFolderId.value = ''
    favoritePosts.value = []
  }
  await loadFavoriteFolders()
  await loadFavoritePosts()
}

const handleRemoveFavorite = async (post) => {
  if (!selectedFolderId.value) return
  await removePostFromFavoriteFolder(selectedFolderId.value, post.postID)
  await Promise.all([loadFavoriteFolders(), loadFavoritePosts()])
}

const handleRemoveFavoriteFromCurrent = async (post) => {
  if (!selectedFolderId.value) return
  await removePostFromFavoriteFolder(selectedFolderId.value, post.postID)
  updatePostFavoriteState(post.postID, false)
  await Promise.all([loadFavoriteFolders(), loadFavoritePosts()])
}

const openPostDetail = async (post) => {
  try {
    detailOpen.value = true
    detailLoading.value = true
    error.value = null
    const res = await getPost(post.postID)
    selectedPost.value = res.data
    await loadComments()
  } catch (e) {
    error.value = '无法加载帖子详情: ' + (e.response?.data?.message || e.message)
  } finally {
    detailLoading.value = false
  }
}

const closePostDetail = () => {
  detailOpen.value = false
  selectedPost.value = null
  comments.value = []
  commentText.value = ''
  cancelReply()
}

const loadComments = async () => {
  if (!selectedPost.value) return

  try {
    commentsLoading.value = true
    const res = await getPostComments(selectedPost.value.postID)
    comments.value = res.data
  } catch (e) {
    error.value = '无法加载评论: ' + (e.response?.data?.message || e.message)
  } finally {
    commentsLoading.value = false
  }
}

const handleCreateComment = async (parentCommentId) => {
  if (!selectedPost.value) return

  const content = parentCommentId ? replyText.value : commentText.value
  if (!content.trim()) return

  try {
    commentSubmitting.value = true
    error.value = null
    const res = await createComment(selectedPost.value.postID, {
      content: content.trim(),
      parentCommentID: parentCommentId,
    })
    notice.value = res.data?.status === 'PendingReview'
      ? '评论已提交审核：内容命中敏感词，暂不会公开展示。'
      : '评论发布成功。'
    commentText.value = ''
    cancelReply()
    await loadComments()
    const refreshed = await getPost(selectedPost.value.postID)
    selectedPost.value = refreshed.data
  } catch (e) {
    error.value = '评论失败: ' + (e.response?.data?.message || e.message)
  } finally {
    commentSubmitting.value = false
  }
}

const startReply = (comment) => {
  replyingTo.value = comment.commentID
  replyText.value = `@${comment.username || '用户'} `
}

const cancelReply = () => {
  replyingTo.value = null
  replyText.value = ''
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
                onInput: (event) => emit('update-reply', event.target.value),
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
              onUpdateReply: (value) => emit('update-reply', value),
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

watch(activeTab, async (tab) => {
  if (tab === 'my-posts') await loadMyPosts()
  if (tab === 'favorites') {
    await loadFavoriteFolders()
    await loadFavoritePosts()
  }
})

onMounted(async () => {
  try {
    await Promise.all([loadForums(), loadPosts(), loadFavoriteFolders(), loadTagStats()])
  } catch (e) {
    error.value = '无法加载论坛数据: ' + (e.response?.data?.message || e.message)
    loading.value = false
  }
})
</script>

<style scoped>
.forum-layout {
  display: grid;
  grid-template-columns: 240px minmax(0, 760px);
  gap: 0;
  align-items: start;
  justify-content: center;
}

.forum-sidebar {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 0;
  padding: 0.75rem;
  position: sticky;
  top: 1rem;
}

.section-title {
  font-weight: 700;
  margin-bottom: 0.5rem;
}

.forum-filter {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border: none;
  background: transparent;
  border-radius: var(--radius);
  padding: 0.625rem 0.75rem;
  color: var(--text);
  cursor: pointer;
  text-align: left;
}

.forum-filter:hover,
.forum-filter.active {
  background: var(--bg);
  color: var(--primary);
}

.forum-count {
  color: var(--text-secondary);
  font-size: 0.75rem;
}

.favorites-layout {
  display: grid;
  grid-template-columns: 240px minmax(0, 760px);
  gap: 0;
  align-items: start;
  justify-content: center;
}

.favorites-sidebar {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 0;
  padding: 0.75rem;
  position: sticky;
  top: 1rem;
}

.favorites-main {
  display: flex;
  flex-direction: column;
}

.folder-create-form {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}

.folder-create-form input {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  flex: 1;
  font: inherit;
  min-width: 0;
  padding: 0.5rem 0.625rem;
  font-size: 0.8125rem;
}

.folder-create-form input:focus {
  border-color: #1d9bf0;
  outline: none;
  box-shadow: 0 0 0 3px rgba(29, 155, 240, 0.12);
}

.folder-list {
  display: flex;
  flex-direction: column;
}

.folder-item {
  border-radius: var(--radius);
  cursor: pointer;
  padding: 0.625rem 0.75rem;
  transition: background 0.15s;
}

.folder-item:hover {
  background: var(--bg);
}

.folder-item.active {
  background: var(--bg);
  color: var(--primary);
}

.folder-item-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.folder-name {
  font-size: 0.875rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.folder-count {
  color: var(--text-secondary);
  font-size: 0.75rem;
  flex-shrink: 0;
}

.folder-item.active .folder-count {
  color: var(--primary);
}

.folder-rename-input {
  border: 1px solid #1d9bf0;
  border-radius: var(--radius);
  font: inherit;
  font-size: 0.8125rem;
  padding: 0.25rem 0.5rem;
  width: 100%;
}

.folder-rename-input:focus {
  outline: none;
  box-shadow: 0 0 0 3px rgba(29, 155, 240, 0.12);
}

.folder-item-actions {
  display: flex;
  gap: 0.75rem;
  margin-top: 0.375rem;
  padding-left: 0.125rem;
}

.folder-action-link {
  color: #536471;
  cursor: pointer;
  font-size: 0.75rem;
}

.folder-action-link:hover {
  color: #0f1419;
  text-decoration: underline;
}

.folder-action-link.danger:hover {
  color: #dc2626;
}

.folder-list-empty {
  color: var(--text-secondary);
  font-size: 0.8125rem;
  padding: 1rem 0.75rem;
  text-align: center;
}

.forum-main,
.post-list {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.feed-toolbar {
  background: var(--surface);
  border: 1px solid var(--border);
  border-left: none;
  display: flex;
  gap: 0.75rem;
  padding: 1rem;
  position: sticky;
  top: 0;
  z-index: 2;
}

.toolbar,
.composer-row {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.toolbar {
  flex: 1;
  min-width: 0;
}

.toolbar input,
.toolbar select,
.composer-fields input,
.composer-fields select,
.composer-fields textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 0.625rem 0.75rem;
  font: inherit;
}

.toolbar input,
.composer-fields input,
.composer-fields select {
  min-width: 0;
}

.toolbar input {
  flex: 1;
}

.composer-row input {
  flex: 1;
}

.compose-trigger,
.compose-submit {
  align-items: center;
  background: #0f1419;
  border: none;
  border-radius: 9999px;
  color: white;
  cursor: pointer;
  display: inline-flex;
  font: inherit;
  font-weight: 700;
  justify-content: center;
  padding: 0.625rem 1.25rem;
  white-space: nowrap;
}

.compose-trigger:hover,
.compose-submit:hover {
  background: #272c30;
}

.compose-submit:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.post-item {
  background: var(--surface);
  border: 1px solid var(--border);
  border-left: none;
  border-radius: 0;
  cursor: pointer;
  padding: 1rem 1.25rem;
  transition: background 0.15s;
}

.post-item + .post-item {
  border-top: none;
}

.post-item:hover,
.post-item:focus-visible {
  background: #f7f9f9;
}

.post-item:focus-visible {
  outline: 2px solid #1d9bf0;
  outline-offset: -2px;
}

.post-item h2 {
  font-size: 1.125rem;
  margin: 0.5rem 0;
}

.post-title-button {
  background: transparent;
  border: none;
  color: #0f1419;
  cursor: pointer;
  display: block;
  font: inherit;
  font-size: 1.125rem;
  font-weight: 700;
  margin: 0.5rem 0;
  padding: 0;
  text-align: left;
  width: 100%;
}

.post-title-button:hover {
  text-decoration: underline;
}

.post-item p {
  color: #536471;
}

.post-meta,
.post-actions,
.tag-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.post-actions {
  margin-top: 0.75rem;
  justify-content: space-between;
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

.tag {
  color: #1d9bf0;
  background: #eff6ff;
  border-radius: 9999px;
  padding: 0.125rem 0.5rem;
  font-size: 0.75rem;
}

.image-strip,
.detail-images {
  display: grid;
  gap: 0.5rem;
  margin: 0.75rem 0;
}

.image-strip {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.image-strip img,
.detail-images img {
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  object-fit: cover;
  width: 100%;
}

.image-strip img {
  aspect-ratio: 4 / 3;
}

.image-preview-item {
  position: relative;
}

.image-remove {
  align-items: center;
  background: rgba(0, 0, 0, 0.68);
  border: none;
  border-radius: 9999px;
  color: #fff;
  cursor: pointer;
  display: inline-flex;
  font-size: 0.75rem;
  padding: 0.25rem 0.5rem;
  position: absolute;
  right: 0.375rem;
  top: 0.375rem;
}

.detail-images {
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
}

.image-preview-item .image-remove {
  opacity: 0;
  transition: opacity 0.15s;
}

.image-preview-item:hover .image-remove,
.image-preview-item:focus-within .image-remove {
  opacity: 1;
}

.field-hint {
  color: var(--text-secondary);
  font-size: 0.75rem;
  margin: 0;
}

.detail-images img {
  aspect-ratio: 16 / 10;
}

.link-button {
  border: none;
  background: transparent;
  color: #536471;
  cursor: pointer;
  font: inherit;
  padding: 0.125rem 0;
}

.link-button:hover {
  color: #1d9bf0;
}

.link-button.danger {
  color: #dc2626;
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

.modal-header,
.post-detail,
.comment-section,
.composer-shell {
  background: var(--surface);
  padding: 1rem;
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
}

.detail-header {
  align-items: center;
  border-bottom: 1px solid var(--border);
  display: flex;
  gap: 1rem;
  justify-content: space-between;
  padding: 1rem;
}

.post-detail {
  border-bottom: 1px solid var(--border);
}

.post-detail h2 {
  font-size: 1.5rem;
  margin: 0.75rem 0;
}

.post-content {
  color: var(--text);
  line-height: 1.7;
  white-space: pre-wrap;
}

.comment-section h3 {
  font-size: 1rem;
  margin-bottom: 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid var(--border);
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

.composer-modal {
  max-width: 620px;
}

.composer-shell {
  display: flex;
  gap: 0.75rem;
}

.composer-avatar {
  align-items: center;
  background: #1d9bf0;
  border-radius: 50%;
  color: white;
  display: flex;
  flex: 0 0 44px;
  font-weight: 800;
  height: 44px;
  justify-content: center;
  width: 44px;
}

.composer-fields {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 0.75rem;
  min-width: 0;
}

.composer-fields textarea {
  border: none;
  border-bottom: 1px solid var(--border);
  border-radius: 0;
  font-size: 1.125rem;
  min-height: 160px;
  padding: 0.5rem 0;
  resize: vertical;
}

.composer-fields textarea:focus,
.composer-fields input:focus,
.composer-fields select:focus,
.toolbar input:focus,
.toolbar select:focus {
  border-color: #1d9bf0;
  outline: none;
  box-shadow: 0 0 0 3px rgba(29, 155, 240, 0.12);
}

.composer-fields textarea:focus {
  box-shadow: none;
}

.composer-tools {
  display: flex;
  justify-content: flex-start;
}

.btn-sm {
  padding: 0.375rem 0.75rem;
  font-size: 0.8125rem;
}

.compact {
  min-height: auto;
  padding: 1rem;
}

.muted {
  color: var(--text-secondary);
}

@media (max-width: 900px) {
  .forum-layout,
  .favorites-layout {
    grid-template-columns: 1fr;
  }

  .forum-sidebar,
  .favorites-sidebar {
    position: static;
  }

  .feed-toolbar {
    border-left: 1px solid var(--border);
    flex-direction: column;
    position: static;
  }

  .toolbar,
  .composer-row {
    align-items: stretch;
    flex-direction: column;
  }

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

.folder-picker-panel {
  max-width: 400px;
}

.folder-pick-list {
  list-style: none;
  margin: 0;
  padding: 0;
  max-height: 300px;
  overflow-y: auto;
}

.folder-pick-item {
  align-items: center;
  border-bottom: 1px solid var(--border);
  cursor: pointer;
  display: flex;
  justify-content: space-between;
  padding: 0.875rem 1rem;
  transition: background 0.15s;
}

.folder-pick-item:last-child {
  border-bottom: none;
}

.folder-pick-item:hover,
.folder-pick-item:focus-visible {
  background: rgba(29, 155, 240, 0.06);
  outline: none;
}

.folder-post-count {
  color: #536471;
  font-size: 0.8125rem;
}

.folder-pick-empty {
  color: #536471;
  padding: 1.5rem 1rem;
  text-align: center;
}

.folder-picker-form {
  border-top: 1px solid var(--border);
  display: flex;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
}

.folder-picker-form input {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  flex: 1;
  font: inherit;
  min-width: 0;
  padding: 0.5rem 0.75rem;
}

.folder-picker-form input:focus {
  border-color: #1d9bf0;
  outline: none;
  box-shadow: 0 0 0 3px rgba(29, 155, 240, 0.12);
}
</style>

<style>
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

.comment-status {
  font-size: 0.6875rem;
  color: #d97706;
  background: #fef3c7;
  border-radius: 9999px;
  padding: 0.0625rem 0.5rem;
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
</style>


<style scoped>
.advanced-search {
  margin: 0 0 1rem;
  padding: 0.75rem;
  border: 1px solid var(--border);
  background: var(--surface);
}

.advanced-search summary {
  cursor: pointer;
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.advanced-search-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.advanced-search-grid label {
  display: grid;
  gap: 0.35rem;
  color: var(--text-secondary);
  font-size: 0.8rem;
}

.advanced-search-actions, .pagination-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.pagination-row {
  justify-content: center;
  padding: 1rem 0;
  color: var(--text-secondary);
  font-size: 0.9rem;
}

@media (max-width: 760px) {
  .advanced-search-grid { grid-template-columns: 1fr; }
}
.success-message {
  background: #dcfce7;
  color: #166534;
  padding: 0.75rem 1rem;
  border-radius: var(--radius);
  margin-bottom: 1rem;
}
</style>

<style scoped>
.forum-page {
  --forum-ink: #11172a;
  --forum-purple: #6957f5;
  width: 100%;
  max-width: 1480px;
  margin: 0 auto;
  color: var(--forum-ink);
}

.forum-hero {
  position: relative;
  isolation: isolate;
  min-height: 330px;
  display: grid;
  grid-template-columns: minmax(0, 1.15fr) minmax(340px, .85fr);
  align-items: center;
  gap: clamp(2rem, 5vw, 5rem);
  overflow: hidden;
  padding: clamp(2rem, 4.5vw, 4.25rem);
  border-radius: 30px;
  color: #fff;
  background: radial-gradient(circle at 16% 0%, rgba(173,148,255,.35), transparent 31%), linear-gradient(135deg, #201b4c 0%, #382b78 48%, #674bdd 100%);
  box-shadow: 0 28px 65px rgba(50,37,116,.21);
}
.forum-hero::before { content: ''; position: absolute; inset: 0; z-index: -1; opacity: .15; background-image: linear-gradient(rgba(255,255,255,.17) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.17) 1px, transparent 1px); background-size: 42px 42px; mask-image: linear-gradient(to right, #000, transparent 75%); }
.forum-hero::after { content: ''; position: absolute; z-index: -1; width: 300px; height: 300px; right: -90px; top: -160px; border-radius: 50%; background: rgba(77,223,235,.19); }
.forum-eyebrow { display: inline-flex; align-items: center; gap: .65rem; color: #d5ceff; font-size: .68rem; font-weight: 800; letter-spacing: .18em; }
.forum-eyebrow i { width: 8px; height: 8px; border-radius: 50%; background: #6fffc0; box-shadow: 0 0 0 6px rgba(111,255,192,.11), 0 0 20px rgba(111,255,192,.75); }
.forum-hero h1 { max-width: 720px; margin-top: 1.25rem; font-size: clamp(2.5rem, 4.8vw, 4.7rem); line-height: 1; letter-spacing: -.055em; }
.forum-hero h1 span { color: #d6ceff; }
.forum-hero-copy > p { max-width: 620px; margin-top: 1.25rem; color: rgba(255,255,255,.68); font-size: .95rem; line-height: 1.8; }
.forum-hero-action { display: inline-flex; align-items: center; gap: .5rem; margin-top: 1.5rem; padding: .78rem 1.15rem; border: 0; border-radius: 13px; color: #382b78; background: #fff; box-shadow: 0 12px 28px rgba(10,7,39,.22); font: inherit; font-size: .84rem; font-weight: 750; cursor: pointer; transition: transform .2s, box-shadow .2s; }
.forum-hero-action:hover { transform: translateY(-2px); box-shadow: 0 16px 34px rgba(10,7,39,.3); }
.forum-hero-action span { font-size: 1.2rem; line-height: 1; }

.forum-pulse-card { padding: 1.4rem; border: 1px solid rgba(255,255,255,.17); border-radius: 24px; background: rgba(10,8,39,.35); box-shadow: 0 22px 50px rgba(6,4,31,.25); backdrop-filter: blur(22px); }
.pulse-label { color: #afa5ef; font-size: .62rem; font-weight: 800; letter-spacing: .14em; }
.forum-pulse-card > strong { display: block; margin-top: .45rem; font-size: 1.2rem; }
.pulse-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: .55rem; margin-top: 1.1rem; }
.pulse-grid > span { display: flex; flex-direction: column; padding: .9rem .7rem; border-radius: 14px; background: rgba(255,255,255,.075); }
.pulse-grid b { font-size: 1.25rem; letter-spacing: -.03em; }
.pulse-grid small { margin-top: .2rem; color: rgba(255,255,255,.45); font-size: .62rem; }
.pulse-live { display: flex; align-items: center; gap: .5rem; margin-top: .8rem; padding: .7rem .8rem; border-radius: 12px; color: rgba(255,255,255,.68); background: rgba(255,255,255,.05); font-size: .68rem; }
.pulse-live i { width: 7px; height: 7px; border-radius: 50%; background: #68f5c8; box-shadow: 0 0 12px rgba(104,245,200,.8); }

.forum-nav { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin: 1.25rem 0; padding: .55rem; border: 1px solid rgba(24,32,55,.08); border-radius: 18px; background: #fff; box-shadow: 0 10px 30px rgba(28,34,64,.05); }
.forum-nav .tabs { gap: .35rem; }
.forum-nav .tab { border: 0; border-radius: 12px; padding: .72rem 1.05rem; color: #70778a; background: transparent; font-weight: 700; }
.forum-nav .tab.active { color: #fff; background: linear-gradient(135deg, #5f50dc, #7967f3); box-shadow: 0 8px 20px rgba(95,80,220,.22); }
.forum-nav-note { padding-right: .8rem; color: #9a9fb0; font-size: .72rem; }

.forum-page .forum-layout, .forum-page .favorites-layout { grid-template-columns: 250px minmax(0, 1fr); gap: 1rem; align-items: start; justify-content: stretch; }
.forum-page .forum-sidebar, .forum-page .favorites-sidebar { top: 1rem; padding: 1rem; border: 1px solid rgba(25,34,59,.08); border-radius: 20px; box-shadow: 0 12px 34px rgba(29,35,58,.05); }
.forum-page .section-title { padding: .25rem .65rem; color: #9b91dc; font-size: .62rem; letter-spacing: .15em; text-transform: uppercase; }
.forum-page .forum-filter { margin: .2rem 0; border-radius: 12px; padding: .7rem .75rem; font-size: .82rem; }
.forum-page .forum-filter:hover { background: #f4f2ff; }
.forum-page .forum-filter.active { color: #5d4dd7; background: #efedff; font-weight: 700; }
.forum-page .forum-count { min-width: 25px; padding: .2rem .38rem; border-radius: 999px; background: rgba(105,87,245,.08); text-align: center; }

.forum-page .forum-main, .forum-page .favorites-main, .forum-page .tab-content { min-width: 0; }
.forum-page .feed-toolbar { top: 1rem; gap: .7rem; padding: .8rem; border: 1px solid rgba(25,34,59,.08); border-radius: 18px; background: rgba(255,255,255,.94); box-shadow: 0 12px 34px rgba(29,35,58,.07); backdrop-filter: blur(16px); }
.forum-page .toolbar input, .forum-page .toolbar select, .forum-page .composer-fields input, .forum-page .composer-fields select, .forum-page .composer-fields textarea, .forum-page .folder-create-form input, .forum-page .folder-rename-input, .forum-page .folder-picker-form input { border: 1px solid #e2e4ed; border-radius: 11px; background: #fafafd; }
.forum-page .toolbar input:focus, .forum-page .toolbar select:focus, .forum-page .composer-fields input:focus, .forum-page .composer-fields select:focus { border-color: #7463ee; box-shadow: 0 0 0 4px rgba(105,87,245,.1); }
.forum-page .compose-trigger, .forum-page .compose-submit { border-radius: 12px; background: linear-gradient(135deg, #5f50dc, #7563ef); box-shadow: 0 8px 20px rgba(95,80,220,.2); }
.forum-page .compose-trigger:hover, .forum-page .compose-submit:hover { background: linear-gradient(135deg, #5142ca, #6956e7); }
.forum-page .advanced-search { overflow: hidden; margin-top: .75rem; padding: .85rem 1rem; border: 1px solid rgba(25,34,59,.08); border-radius: 16px; background: #fff; }
.forum-page .advanced-search summary { color: #6654d7; font-weight: 650; }
.forum-page .advanced-search :is(input, select) { border: 1px solid #e2e4ed; border-radius: 10px; padding: .6rem; font: inherit; }

.forum-page .post-list { gap: .75rem; margin-top: .85rem; }
.forum-page .post-item { padding: 1.25rem 1.35rem; border: 1px solid rgba(25,34,59,.08); border-radius: 20px; background: #fff; box-shadow: 0 10px 30px rgba(29,35,58,.045); transition: transform .22s, box-shadow .22s, border-color .22s; }
.forum-page .post-item + .post-item { border-top: 1px solid rgba(25,34,59,.08); }
.forum-page .post-item:hover, .forum-page .post-item:focus-visible { transform: translateY(-3px); border-color: rgba(105,87,245,.2); background: #fff; box-shadow: 0 18px 42px rgba(45,39,99,.1); }
.forum-page .post-title-button { margin: .7rem 0 .5rem; color: #151a2b; font-size: 1.18rem; letter-spacing: -.025em; }
.forum-page .post-title-button:hover { color: #5d4dd7; text-decoration: none; }
.forum-page .post-item p { color: #70778a; line-height: 1.7; }
.forum-page .post-meta > span:not(.badge) { position: relative; }
.forum-page .tag { color: #6654d7; background: #f0eeff; }
.forum-page .post-actions { max-width: none; padding-top: .65rem; border-top: 1px solid #f0f1f5; }
.forum-page .post-metric { color: #8a90a0; }
.forum-page .post-icon-action:hover, .forum-page .post-icon-action:focus-visible { color: #6654e8; background: #f0eeff; }
.forum-page .image-strip { overflow: hidden; border-radius: 15px; }
.forum-page .image-strip img { border: 0; border-radius: 0; }
.forum-page .pagination-row { padding: 1rem; border-radius: 16px; background: #fff; }
.forum-page .empty-state { border: 1px dashed #d9dbe5; border-radius: 20px; background: #fafaff; }

.forum-page .folder-create-form { display: grid; grid-template-columns: 1fr; }
.forum-page .folder-item { margin: .2rem 0; border-radius: 12px; }
.forum-page .folder-item:hover { background: #f6f4ff; }
.forum-page .folder-item.active { color: #5d4dd7; background: #efedff; }
.forum-page .folder-item-actions { opacity: .68; }
.forum-page .favorites-main > .post-list, .forum-page .tab-content > .post-list { margin-top: 0; }

.forum-page .detail-backdrop { padding: 2rem 1rem; background: rgba(18,16,42,.58); backdrop-filter: blur(6px); }
.forum-page .post-detail-panel { max-width: 760px; max-height: calc(100vh - 4rem); border: 1px solid rgba(255,255,255,.15); border-radius: 22px; box-shadow: 0 28px 80px rgba(13,10,40,.3); }
.forum-page .modal-header, .forum-page .detail-header { background: rgba(255,255,255,.96); backdrop-filter: blur(14px); }
.forum-page .composer-avatar { background: linear-gradient(135deg, #6654ee, #20b9bb); box-shadow: 0 8px 20px rgba(102,84,238,.22); }
.forum-page .composer-fields textarea { border: 0; border-bottom: 1px solid #e7e8ee; border-radius: 0; background: #fff; }
.forum-page .post-detail h2 { letter-spacing: -.035em; }
.forum-page .comment-section { background: #fbfbfe; }
.forum-page .folder-pick-item:hover, .forum-page .folder-pick-item:focus-visible { background: #f2f0ff; }

@media (max-width: 1000px) {
  .forum-page .forum-layout, .forum-page .favorites-layout { grid-template-columns: 215px minmax(0, 1fr); }
  .forum-page .toolbar { flex-wrap: wrap; }
  .forum-page .toolbar input { flex-basis: 42%; }
}
@media (max-width: 820px) {
  .forum-hero { grid-template-columns: 1fr; padding: 2rem; border-radius: 24px; }
  .forum-pulse-card { max-width: 620px; }
  .forum-page .forum-layout, .forum-page .favorites-layout { grid-template-columns: 1fr; }
  .forum-page .forum-sidebar, .forum-page .favorites-sidebar { position: static; }
  .forum-page .forum-sidebar { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .forum-page .forum-sidebar .section-title { grid-column: 1 / -1; }
  .forum-page .feed-toolbar { position: static; }
}
@media (max-width: 640px) {
  .forum-hero { min-height: auto; gap: 1.5rem; padding: 1.45rem; border-radius: 20px; }
  .forum-hero h1 { font-size: 2.05rem; }
  .forum-hero-copy > p { font-size: .86rem; }
  .forum-pulse-card { padding: 1rem; border-radius: 18px; }
  .forum-nav { align-items: stretch; overflow-x: auto; }
  .forum-nav .tabs { flex-wrap: nowrap; }
  .forum-nav .tab { white-space: nowrap; }
  .forum-nav-note { display: none; }
  .forum-page .forum-sidebar { display: flex; gap: .4rem; overflow-x: auto; padding: .65rem; scrollbar-width: thin; }
  .forum-page .forum-sidebar .section-title { display: none; }
  .forum-page .forum-filter { flex: 0 0 auto; width: auto; gap: .5rem; margin: 0; white-space: nowrap; }
  .forum-page .feed-toolbar, .forum-page .toolbar { align-items: stretch; flex-direction: column; }
  .forum-page .toolbar input { flex-basis: auto; width: 100%; }
  .forum-page .post-item { padding: 1rem; border-radius: 17px; }
  .forum-page .post-actions { justify-content: flex-start; gap: .25rem; }
  .forum-page .post-metric { font-size: .72rem; }
  .forum-page .detail-backdrop { padding: 0; }
  .forum-page .post-detail-panel { max-height: 100vh; border-radius: 0; }
  .forum-page .composer-shell { padding: .85rem; }
  .pulse-grid > span { padding: .75rem .55rem; }
}
</style>

<style>
.forum-page .comment-form textarea, .forum-page .reply-form textarea { border: 1px solid #e1e3ec; border-radius: 12px; background: #fff; }
.forum-page .comment-form textarea:focus, .forum-page .reply-form textarea:focus { border-color: #7463ee; box-shadow: 0 0 0 4px rgba(105,87,245,.1); }
.forum-page .comment-node { border-bottom-color: #e9eaf0; }
.forum-page .comment-avatar { background: linear-gradient(135deg, #6654ee, #20b9bb); box-shadow: 0 6px 15px rgba(102,84,238,.16); }
.forum-page .comment-author { color: #30364a; font-weight: 650; }
.forum-page .comment-content { color: #33394d; }
.forum-page .comment-action-link:hover { color: #6654e8; }
.forum-page .comment-children { border-left-color: #dcd7ff; }
@media (max-width: 640px) {
  .forum-page .comment-children { margin-left: 18px; padding-left: .7rem; }
}
</style>
