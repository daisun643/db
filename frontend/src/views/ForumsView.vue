<template>
  <div class="page-container forum-page">
    <div class="forum-nav">
      <ForumTabs v-model="activeTab" />
    </div>

    <div v-if="error" class="error-message">{{ error }}</div>
    <div v-if="notice" class="success-message">{{ notice }}</div>

    <ForumCreatorModal
      :open="forumCreatorOpen"
      :form="forumForm"
      :creating="creatingForum"
      @close="closeForumCreator"
      @submit="handleCreateForum"
      @update:form="forumForm = $event"
    />

    <AllForumPanel
      v-if="activeTab === 'all'"
      ref="allPanel"
      :forums="forums"
      @open-forum-creator="openForumCreator"
      @open-composer="openComposer"
      @open-post="openPostDetail"
      @like="handleLike"
      @favorite="handleFavorite"
      @report="openReport"
      @error="error = $event"
    />

    <MyPostsPanel
      v-else-if="activeTab === 'my-posts'"
      ref="myPostsPanel"
      @open-post="openPostDetail"
      @like="handleLike"
      @edit-post="openEditPost"
      @delete-post="handleDeletePost"
      @error="error = $event"
    />

    <MyForumsPanel
      v-else-if="activeTab === 'my-forums'"
      @forums-changed="loadForums"
      @notice="notice = $event"
      @error="error = $event"
    />

    <FavoritesPanel
      v-else-if="activeTab === 'favorites'"
      ref="favoritesPanel"
      :favorite-folders="favoriteFolders"
      @open-post="openPostDetail"
      @like="handleLike"
      @folders-changed="loadFavoriteFolders"
      @error="error = $event"
    />

    <PostComposerModal
      :open="composerOpen"
      :forums="forums"
      @close="closeComposer"
      @created="handlePostCreated"
      @error="error = $event"
    />

    <PostDetailModal
      :open="detailOpen"
      :post="selectedPost"
      :detail-loading="detailLoading"
      :comments="comments"
      :comments-loading="commentsLoading"
      :favorite-folders="favoriteFolders"
      v-model:commentText="commentText"
      :comment-submitting="commentSubmitting"
      :replying-to="replyingTo"
      v-model:replyText="replyText"
      @close="closePostDetail"
      @like="handleLike"
      @favorite="handleFavorite"
      @edit="openEditPost"
      @report="openReport"
      @submit-comment="handleCreateComment"
      @reply="startReply"
      @cancel-reply="cancelReply"
      @report-comment="openCommentReport"
      @delete-comment="handleDeleteComment"
    />

    <PostEditorModal
      :open="!!editingPost"
      :post="editingPost"
      @close="closeEditPost"
      @saved="handleEditorSaved"
      @error="error = $event"
    />

    <ReportModal
      :open="!!reportTarget"
      :target="reportTarget"
      v-model:reason="reportReason"
      @close="reportTarget = null"
      @submit="handleCreateReport"
    />

    <FolderPickerModal
      :open="folderPickerOpen"
      :folders="favoriteFolders"
      v-model:pickerFolderName="pickerFolderName"
      @close="closeFolderPicker"
      @select="selectFolderForFavorite"
      @create="handlePickerCreateFolder"
    />
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useAuthStore } from '../stores/auth'
import ForumCreatorModal from '../components/ForumCreatorModal.vue'
import ForumTabs from '../components/forum/ForumTabs.vue'
import AllForumPanel from '../tab/forum/AllForumPanel.vue'
import MyPostsPanel from '../tab/forum/MyPostsPanel.vue'
import MyForumsPanel from '../tab/forum/MyForumsPanel.vue'
import FavoritesPanel from '../tab/forum/FavoritesPanel.vue'
import PostComposerModal from '../components/forum/PostComposerModal.vue'
import PostDetailModal from '../components/forum/PostDetailModal.vue'
import PostEditorModal from '../components/forum/PostEditorModal.vue'
import ReportModal from '../components/forum/ReportModal.vue'
import FolderPickerModal from '../components/forum/FolderPickerModal.vue'
import {
  addPostToFavoriteFolder,
  createComment,
  createFavoriteFolder,
  createForum,
  createReport,
  deleteComment,
  deletePost,
  getFavoriteFolders,
  getForums,
  getPost,
  getPostComments,
  likePost,
  unfavoritePost,
  unlikePost,
} from '../api'

const authStore = useAuthStore()
const activeTab = ref('all')
const forums = ref([])
const favoriteFolders = ref([])
const detailLoading = ref(false)
const commentsLoading = ref(false)
const commentSubmitting = ref(false)
const composerOpen = ref(false)
const forumCreatorOpen = ref(false)
const creatingForum = ref(false)
const error = ref(null)
const notice = ref('')
const detailOpen = ref(false)
const selectedPost = ref(null)
const comments = ref([])
const commentText = ref('')
const replyingTo = ref(null)
const replyText = ref('')
const editingPost = ref(null)
const reportTarget = ref(null)
const reportReason = ref('')
const folderPickerOpen = ref(false)
const folderPickerTarget = ref(null)
const pickerFolderName = ref('')
const allPanel = ref(null)
const myPostsPanel = ref(null)
const favoritesPanel = ref(null)

const forumForm = ref({
  forumName: '',
  description: '',
})

const loadForums = async () => {
  const res = await getForums()
  forums.value = res.data
}

const openForumCreator = () => {
  error.value = null
  forumCreatorOpen.value = true
}

const closeForumCreator = () => {
  if (creatingForum.value) return
  forumCreatorOpen.value = false
}

const handleCreateForum = async () => {
  try {
    creatingForum.value = true
    error.value = null
    const res = await createForum({
      forumName: forumForm.value.forumName.trim(),
      description: forumForm.value.description.trim(),
    })
    forumForm.value = { forumName: '', description: '' }
    forumCreatorOpen.value = false
    notice.value = '版块创建成功。'
    await loadForums()
    allPanel.value?.selectForum(res.data.forumID)
  } catch (e) {
    error.value = '创建版块失败: ' + (e.response?.data?.message || e.message)
  } finally {
    creatingForum.value = false
  }
}

const loadFavoriteFolders = async () => {
  const res = await getFavoriteFolders()
  favoriteFolders.value = res.data
}



const openComposer = () => {
  error.value = null
  composerOpen.value = true
}

const closeComposer = () => {
  composerOpen.value = false
}

const handlePostCreated = (post) => {
  notice.value = post?.status === 'PendingReview'
    ? '帖子已提交审核：内容命中敏感词，暂不会公开展示；可在“我的帖子”查看审核状态。'
    : '帖子发布成功。'
  reloadMountedFeeds()
}

const handleLike = async (post) => {
  const res = post.isLiked ? await unlikePost(post.postID) : await likePost(post.postID)
  const nextLiked = res.data.liked
  const nextCount = res.data.likeCount
  updatePostLikeState(post.postID, nextLiked, nextCount)
}

const patchMountedPosts = (postId, patch) => {
  for (const panel of [allPanel.value, myPostsPanel.value, favoritesPanel.value]) {
    panel?.applyPostPatch?.(postId, patch)
  }
}

const reloadMountedFeeds = () => {
  allPanel.value?.reload()
  myPostsPanel.value?.reload()
}

const updatePostLikeState = (postId, isLiked, likeCount) => {
  if (selectedPost.value?.postID === postId) {
    selectedPost.value.isLiked = isLiked
    selectedPost.value.likeCount = likeCount
  }
  patchMountedPosts(postId, { isLiked, likeCount })
}

const updatePostFavoriteState = (postId, isFavorited) => {
  if (selectedPost.value?.postID === postId) {
    selectedPost.value.isFavorited = isFavorited
  }
  patchMountedPosts(postId, { isFavorited })
}

const handleFavorite = async (post) => {
  if (post.isFavorited) {
    await unfavoritePost(post.postID)
    updatePostFavoriteState(post.postID, false)
    await loadFavoriteFolders()
    favoritesPanel.value?.reloadPosts()
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
  reloadMountedFeeds()
}

const openEditPost = async (post) => {
  try {
    error.value = null
    const res = await getPost(post.postID)
    editingPost.value = res.data
  } catch (e) {
    error.value = '无法加载编辑内容: ' + (e.response?.data?.message || e.message)
  }
}

const closeEditPost = () => {
  editingPost.value = null
}

const handleEditorSaved = (updatedPost) => {
  if (selectedPost.value?.postID === updatedPost?.postID) {
    selectedPost.value = updatedPost
  }
  reloadMountedFeeds()
  closeEditPost()
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

onMounted(async () => {
  try {
    await Promise.all([loadForums(), loadFavoriteFolders()])
  } catch (e) {
    error.value = '无法加载论坛数据: ' + (e.response?.data?.message || e.message)
  }
})
</script>


<style scoped>
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

.forum-nav { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin: 1.25rem 0; padding: .55rem; border: 1px solid rgba(24,32,55,.08); border-radius: 18px; background: #fff; box-shadow: 0 10px 30px rgba(28,34,64,.05); }

@media (max-width: 640px) {
  .forum-nav { align-items: stretch; overflow-x: auto; }
}
</style>

