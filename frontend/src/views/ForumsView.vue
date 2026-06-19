<template>
  <div class="page-container">
    <div class="page-header page-header-tabs">
      <h1 class="page-title">论坛</h1>

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
    </div>

    <div v-if="error" class="error-message">{{ error }}</div>

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
      </aside>

      <main class="forum-main">
        <div class="feed-toolbar">
          <div class="toolbar">
            <input v-model="filters.keyword" type="search" placeholder="搜索标题或内容" @keyup.enter="loadPosts" />
            <input v-model="filters.tag" type="search" placeholder="标签" @keyup.enter="loadPosts" />
            <select v-model="filters.sort" @change="loadPosts">
              <option value="latest">最新</option>
              <option value="hot">热度</option>
            </select>
            <button class="btn btn-primary" @click="loadPosts">筛选</button>
          </div>
          <button class="compose-trigger" @click="openComposer">发布帖子</button>
        </div>

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
              <span>热度 {{ post.heatScore || 0 }}</span>
              <span>浏览 {{ post.viewCount || 0 }}</span>
              <span>点赞 {{ post.likeCount || 0 }}</span>
              <span>评论 {{ post.commentCount || 0 }}</span>
              <button class="link-button" @click.stop="handleLike(post)">
                {{ post.isLiked ? '取消点赞' : '点赞' }}
              </button>
              <button class="link-button" @click.stop="openPostDetail(post)">查看详情</button>
              <button class="link-button" @click.stop="handleFavorite(post)" :disabled="favoriteFolders.length === 0">
                收藏
              </button>
              <button class="link-button danger" @click.stop="openReport(post)">举报</button>
            </div>
          </article>
          <div v-if="posts.length === 0" class="empty-state">
            <p>暂无帖子</p>
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

    <div v-else-if="activeTab === 'favorites'" class="tab-content">
      <div class="favorite-header">
        <select v-model.number="selectedFolderId" @change="loadFavoritePosts">
          <option disabled value="">选择收藏夹</option>
          <option v-for="folder in favoriteFolders" :key="folder.folderID" :value="folder.folderID">
            {{ folder.folderName }} ({{ folder.postCount || 0 }})
          </option>
        </select>
        <form @submit.prevent="handleCreateFolder" class="folder-form">
          <input v-model="folderName" type="text" placeholder="新收藏夹名称" required />
          <button class="btn" type="submit">创建</button>
        </form>
        <form v-if="selectedFolderId" @submit.prevent="handleRenameFolder" class="folder-form">
          <input v-model="folderRenameName" type="text" placeholder="重命名收藏夹" required />
          <button class="btn" type="submit">重命名</button>
          <button class="btn" type="button" @click="handleDeleteFolder">删除收藏夹</button>
        </form>
      </div>

      <div v-if="loadingFavorites" class="loading">加载中...</div>
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
          <p>暂无收藏</p>
        </div>
      </div>
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
              <input v-model="imageText" type="text" placeholder="图片 URL，用逗号分隔" />
            </div>
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
              <span>热度 {{ selectedPost.heatScore || 0 }}</span>
              <span>浏览 {{ selectedPost.viewCount || 0 }}</span>
              <span>点赞 {{ selectedPost.likeCount || 0 }}</span>
              <span>评论 {{ selectedPost.commentCount || 0 }}</span>
              <button class="link-button" @click="handleLike(selectedPost)">
                {{ selectedPost.isLiked ? '取消点赞' : '点赞' }}
              </button>
              <button class="link-button" @click="handleFavorite(selectedPost)" :disabled="favoriteFolders.length === 0">
                收藏
              </button>
              <button v-if="canEditPost(selectedPost)" class="link-button" @click="openEditPost(selectedPost)">
                编辑
              </button>
              <button class="link-button danger" @click="openReport(selectedPost)">举报</button>
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
      <form class="post-detail-panel post-edit-form" @submit.prevent="handleUpdatePost">
        <div class="detail-header">
          <button class="link-button" type="button" @click="closeEditPost">取消编辑</button>
          <span class="muted">帖子 #{{ editingPost.postID }}</span>
        </div>
        <h2>编辑帖子</h2>
        <input v-model="editForm.title" type="text" placeholder="帖子标题" required />
        <textarea v-model="editForm.content" placeholder="帖子内容" required></textarea>
        <div class="composer-row">
          <input v-model="editTagText" type="text" placeholder="标签，用逗号分隔" />
          <input v-model="editImageText" type="text" placeholder="图片 URL，用逗号分隔" />
        </div>
        <button class="btn btn-primary" type="submit" :disabled="editingSaving">
          {{ editingSaving ? '保存中...' : '保存帖子' }}
        </button>
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
  </div>
</template>

<script setup>
import { computed, defineComponent, h, onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../stores/auth'
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
  likePost,
  removePostFromFavoriteFolder,
  suggestTags,
  unlikePost,
  updateFavoriteFolder,
  updatePost,
} from '../api'

const authStore = useAuthStore()
const activeTab = ref('all')
const forums = ref([])
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
const tagText = ref('')
const imageText = ref('')
const folderName = ref('')
const folderRenameName = ref('')
const selectedFolderId = ref('')
const detailOpen = ref(false)
const selectedPost = ref(null)
const comments = ref([])
const commentText = ref('')
const replyingTo = ref(null)
const replyText = ref('')
const editingPost = ref(null)
const editingSaving = ref(false)
const editTagText = ref('')
const editImageText = ref('')
const reportTarget = ref(null)
const reportReason = ref('')
const userInitial = computed(() => (authStore.user?.username || '用')[0]?.toUpperCase() || '用')

const filters = ref({
  forumId: null,
  keyword: '',
  tag: '',
  sort: 'latest',
})

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

const loadPosts = async () => {
  try {
    loading.value = true
    const res = await getPosts({
      forumId: filters.value.forumId || undefined,
      keyword: filters.value.keyword || undefined,
      tag: filters.value.tag || undefined,
      sort: filters.value.sort,
    })
    posts.value = res.data
  } catch (e) {
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
  if (!selectedFolderId.value && favoriteFolders.value.length > 0) {
    selectedFolderId.value = favoriteFolders.value[0].folderID
  }
  const selected = favoriteFolders.value.find(folder => folder.folderID === selectedFolderId.value)
  folderRenameName.value = selected?.folderName || ''
}

const loadFavoritePosts = async () => {
  const selected = favoriteFolders.value.find(folder => folder.folderID === selectedFolderId.value)
  folderRenameName.value = selected?.folderName || ''

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
  await loadPosts()
}

const openComposer = () => {
  error.value = null
  composerOpen.value = true
}

const closeComposer = () => {
  if (submitting.value) return
  composerOpen.value = false
}

const handleCreatePost = async () => {
  try {
    submitting.value = true
    error.value = null
    const tagNames = tagText.value.split(/[,，]/).map(tag => tag.trim()).filter(Boolean)
    const imageUrls = imageText.value.split(/[,，]/).map(url => url.trim()).filter(Boolean)
    await createPost({
      ...postForm.value,
      tagNames,
      imageUrls,
    })
    postForm.value.title = ''
    postForm.value.content = ''
    tagText.value = ''
    imageText.value = ''
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

const handleFavorite = async (post) => {
  if (!selectedFolderId.value) return
  await addPostToFavoriteFolder(selectedFolderId.value, post.postID)
  await loadFavoriteFolders()
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
    editForm.value = {
      title: res.data.title || '',
      content: res.data.content || '',
    }
    editTagText.value = (res.data.tags || []).join(', ')
    editImageText.value = (res.data.imageUrls || []).join(', ')
  } catch (e) {
    error.value = '无法加载编辑内容: ' + (e.response?.data?.message || e.message)
  }
}

const closeEditPost = () => {
  editingPost.value = null
  editTagText.value = ''
  editImageText.value = ''
}

const handleUpdatePost = async () => {
  if (!editingPost.value) return

  try {
    editingSaving.value = true
    error.value = null
    const tagNames = editTagText.value.split(/[,，]/).map(tag => tag.trim()).filter(Boolean)
    const imageUrls = editImageText.value.split(/[,，]/).map(url => url.trim()).filter(Boolean)
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
  await createFavoriteFolder({ folderName: folderName.value })
  folderName.value = ''
  await loadFavoriteFolders()
}

const handleRenameFolder = async () => {
  if (!selectedFolderId.value) return
  await updateFavoriteFolder(selectedFolderId.value, { folderName: folderRenameName.value })
  await loadFavoriteFolders()
}

const handleDeleteFolder = async () => {
  if (!selectedFolderId.value) return
  await deleteFavoriteFolder(selectedFolderId.value)
  selectedFolderId.value = ''
  favoritePosts.value = []
  await loadFavoriteFolders()
  await loadFavoritePosts()
}

const handleRemoveFavorite = async (post) => {
  if (!selectedFolderId.value) return
  await removePostFromFavoriteFolder(selectedFolderId.value, post.postID)
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
    await createComment(selectedPost.value.postID, {
      content: content.trim(),
      parentCommentID: parentCommentId,
    })
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

    const renderNode = () => h('article', { class: 'comment-node' }, [
      h('div', { class: 'comment-avatar' }, [
        h('span', initial(props.comment.username)),
      ]),
      h('div', { class: 'comment-body' }, [
        h('div', { class: 'comment-header' }, [
          h('span', { class: 'comment-author' }, props.comment.username || '用户'),
          h('span', { class: 'comment-time' }, formatDate(props.comment.createTime)),
          props.comment.status && props.comment.status !== 'Active'
            ? h('span', { class: 'comment-status' }, props.comment.status)
            : null,
        ]),
        h('div', { class: 'comment-content' }, props.comment.content || ''),
        h('div', { class: 'comment-actions' }, [
          h('button', { class: 'comment-action-btn', onClick: () => emit('reply', props.comment) }, [
            h('svg', { viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', 'stroke-width': '2', class: 'action-icon' }, [
              h('path', { d: 'M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z' }),
            ]),
            '回复',
          ]),
          h('button', { class: 'comment-action-btn danger', onClick: () => emit('report', props.comment) }, '举报'),
          canDelete()
            ? h('button', { class: 'comment-action-btn danger', onClick: () => emit('delete', props.comment) }, '删除')
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
    await Promise.all([loadForums(), loadPosts(), loadFavoriteFolders()])
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
.favorite-header {
  background: transparent;
  border: none;
  border-radius: 0;
  padding: 0;
}

.toolbar,
.composer-row,
.favorite-header,
.folder-form {
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
.composer-fields textarea,
.favorite-header input,
.favorite-header select {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 0.625rem 0.75rem;
  font: inherit;
}

.toolbar input,
.composer-fields input,
.composer-fields select,
.favorite-header input,
.favorite-header select {
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
  max-width: 520px;
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

.detail-images {
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
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
  font-weight: 600;
  font-size: 0.875rem;
  color: var(--text);
}

.comment-time {
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.comment-status {
  font-size: 0.6875rem;
  color: #d97706;
  background: #fef3c7;
  border-radius: 9999px;
  padding: 0.0625rem 0.5rem;
}

.comment-content {
  color: var(--text);
  line-height: 1.65;
  font-size: 0.9375rem;
  white-space: pre-wrap;
  word-break: break-word;
}

.comment-actions {
  display: flex;
  gap: 0.25rem;
  margin-top: 0.5rem;
}

.comment-action-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  font: inherit;
  font-size: 0.8125rem;
  padding: 0.25rem 0.5rem;
  border-radius: var(--radius);
  transition: all 0.15s;
}

.comment-action-btn:hover {
  background: var(--bg);
  color: var(--primary);
}

.comment-action-btn.danger:hover {
  color: #dc2626;
  background: #fef2f2;
}

.action-icon {
  width: 14px;
  height: 14px;
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
  .forum-layout {
    grid-template-columns: 1fr;
  }

  .forum-sidebar {
    position: static;
  }

  .feed-toolbar {
    border-left: 1px solid var(--border);
    flex-direction: column;
    position: static;
  }

  .toolbar,
  .composer-row,
  .favorite-header,
  .folder-form {
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
</style>
