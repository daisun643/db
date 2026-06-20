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
        <div class="toolbar">
          <input v-model="filters.keyword" type="search" placeholder="搜索标题或内容" @keyup.enter="loadPosts" />
          <input v-model="filters.tag" type="search" placeholder="标签" @keyup.enter="loadPosts" />
          <select v-model="filters.sort" @change="loadPosts">
            <option value="latest">最新</option>
            <option value="hot">热度</option>
          </select>
          <button class="btn btn-primary" @click="loadPosts">筛选</button>
        </div>

        <form class="composer" @submit.prevent="handleCreatePost">
          <div class="composer-row">
            <select v-model.number="postForm.forumID" required>
              <option disabled value="">选择版块</option>
              <option v-for="forum in forums" :key="forum.forumID" :value="forum.forumID">
                {{ forum.forumName }}
              </option>
            </select>
            <input v-model="postForm.title" type="text" placeholder="帖子标题" required />
          </div>
          <textarea v-model="postForm.content" placeholder="分享你的内容" required></textarea>
          <div class="composer-row">
            <input v-model="tagText" type="text" placeholder="标签，用逗号分隔" />
            <input v-model="imageText" type="text" placeholder="图片 URL，用逗号分隔" />
            <button class="btn" type="button" @click="handleSuggestTags">推荐标签</button>
            <button class="btn btn-primary" type="submit" :disabled="submitting">
              {{ submitting ? '发布中...' : '发布' }}
            </button>
          </div>
        </form>

        <div v-if="loading" class="loading">加载中...</div>
        <div v-else class="post-list">
          <article v-for="post in posts" :key="post.postID" class="post-item">
            <div class="post-meta">
              <span>{{ post.forumName || '未分区' }}</span>
              <span>{{ post.username || '匿名用户' }}</span>
              <span>{{ formatDate(post.createTime) }}</span>
              <span :class="['badge', post.status === 'Active' ? 'badge-green' : 'badge-yellow']">
                {{ post.status }}
              </span>
            </div>
            <button class="post-title-button" @click="openPostDetail(post)">
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
              <button class="link-button" @click="handleLike(post)">
                {{ post.isLiked ? '取消点赞' : '点赞' }}
              </button>
              <button class="link-button" @click="openPostDetail(post)">查看详情</button>
              <button class="link-button" @click="handleFavorite(post)" :disabled="favoriteFolders.length === 0">
                收藏
              </button>
              <button class="link-button danger" @click="openReport(post)">举报</button>
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
        <article v-for="post in myPosts" :key="post.postID" class="post-item">
          <div class="post-meta">
            <span>{{ post.forumName || '未分区' }}</span>
            <span>{{ formatDate(post.createTime) }}</span>
            <span :class="['badge', post.status === 'Active' ? 'badge-green' : 'badge-yellow']">
              {{ post.status }}
            </span>
          </div>
          <button class="post-title-button" @click="openPostDetail(post)">
            {{ post.title }}
          </button>
          <p>{{ post.contentPreview }}</p>
          <div class="post-actions">
            <button class="link-button" @click="openEditPost(post)">编辑</button>
            <button class="link-button danger" @click="handleDeletePost(post)">删除</button>
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
        <article v-for="post in favoritePosts" :key="post.postID" class="post-item">
          <div class="post-meta">
            <span>{{ post.forumName || '未分区' }}</span>
            <span>{{ post.username || '匿名用户' }}</span>
          </div>
          <button class="post-title-button" @click="openPostDetail(post)">
            {{ post.title }}
          </button>
          <p>{{ post.contentPreview }}</p>
          <button class="link-button danger" @click="handleRemoveFavorite(post)">取消收藏</button>
        </article>
        <div v-if="favoritePosts.length === 0" class="empty-state">
          <p>暂无收藏</p>
        </div>
      </div>
    </div>

    <div v-if="detailOpen" class="detail-backdrop" @click.self="closePostDetail">
      <section class="post-detail-panel">
        <div class="detail-header">
          <button class="link-button" @click="closePostDetail">返回列表</button>
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
import { defineComponent, h, onMounted, ref, watch } from 'vue'
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

    const renderNode = () => h('article', { class: 'comment-node' }, [
      h('div', { class: 'comment-main' }, [
        h('div', { class: 'post-meta' }, [
          h('strong', props.comment.username || '用户'),
          h('span', formatDate(props.comment.createTime)),
          h('span', props.comment.status || ''),
        ]),
        h('p', props.comment.content || ''),
        h('div', { class: 'comment-actions' }, [
          h('button', { class: 'link-button', onClick: () => emit('reply', props.comment) }, '回复'),
          h('button', { class: 'link-button danger', onClick: () => emit('report', props.comment) }, '举报'),
          canDelete()
            ? h('button', { class: 'link-button danger', onClick: () => emit('delete', props.comment) }, '删除')
            : null,
        ]),
        props.replyingTo === props.comment.commentID
          ? h('form', {
              class: 'comment-form reply-form',
              onSubmit: (event) => {
                event.preventDefault()
                emit('submit-reply', props.comment.commentID)
              },
            }, [
              h('textarea', {
                value: props.replyText,
                required: true,
                placeholder: '写下回复',
                onInput: (event) => emit('update-reply', event.target.value),
              }),
              h('div', { class: 'composer-row' }, [
                h('button', { class: 'btn btn-primary', type: 'submit' }, '发送回复'),
                h('button', { class: 'btn', type: 'button', onClick: () => emit('cancel-reply') }, '取消'),
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
  grid-template-columns: 240px minmax(0, 1fr);
  gap: 1rem;
  align-items: start;
}

.forum-sidebar {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 0.75rem;
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
  gap: 1rem;
}

.toolbar,
.composer,
.favorite-header {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.toolbar,
.composer-row,
.favorite-header,
.folder-form {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.toolbar input,
.toolbar select,
.composer input,
.composer select,
.composer textarea,
.favorite-header input,
.favorite-header select {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 0.625rem 0.75rem;
  font: inherit;
}

.toolbar input,
.composer input,
.composer select,
.favorite-header input,
.favorite-header select {
  min-width: 0;
}

.toolbar input {
  flex: 1;
}

.composer {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.composer-row input {
  flex: 1;
}

.composer textarea {
  min-height: 120px;
  resize: vertical;
}

.post-item {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.post-item h2 {
  font-size: 1.125rem;
  margin: 0.5rem 0;
}

.post-title-button {
  background: transparent;
  border: none;
  color: var(--text);
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
  color: var(--primary);
}

.post-item p {
  color: var(--text-secondary);
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
}

.tag {
  color: var(--primary);
  background: var(--bg);
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
  color: var(--primary);
  cursor: pointer;
  font: inherit;
}

.link-button.danger {
  color: #dc2626;
}

.detail-backdrop {
  background: rgba(15, 23, 42, 0.36);
  bottom: 0;
  display: flex;
  justify-content: flex-end;
  left: 0;
  position: fixed;
  right: 0;
  top: 0;
  z-index: 1200;
}

.post-detail-panel {
  background: var(--bg);
  border-left: 1px solid var(--border);
  box-shadow: -12px 0 30px rgba(15, 23, 42, 0.16);
  height: 100vh;
  max-width: 860px;
  overflow-y: auto;
  padding: 1rem;
  width: min(860px, 100vw);
}

.detail-header,
.post-detail,
.comment-section {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
}

.detail-header {
  align-items: center;
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.post-detail {
  margin-bottom: 1rem;
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
}

.comment-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.comment-form textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  min-height: 88px;
  padding: 0.625rem 0.75rem;
  resize: vertical;
}

.comment-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.comment-node {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 0.75rem;
}

.comment-main p {
  color: var(--text);
  line-height: 1.6;
  margin: 0.5rem 0;
  white-space: pre-wrap;
}

.comment-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.comment-children {
  border-left: 2px solid var(--border);
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-left: 0.5rem;
  margin-top: 0.75rem;
  padding-left: 0.75rem;
}

.reply-form {
  margin: 0.75rem 0 0;
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

  .toolbar,
  .composer-row,
  .favorite-header,
  .folder-form {
    align-items: stretch;
    flex-direction: column;
  }

  .detail-backdrop {
    display: block;
  }

  .post-detail-panel {
    border-left: none;
    width: 100vw;
  }

  .detail-header {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
