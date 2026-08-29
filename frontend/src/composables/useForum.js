import { ref } from 'vue'
import { useRouter } from 'vue-router'
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

// 模块级共享状态：论坛布局与各子页面共用同一份数据与弹窗状态
const forums = ref([])
const favoriteFolders = ref([])
const error = ref(null)
const notice = ref('')

// 帖子详情页（/forums/post/:postId）
const detailLoading = ref(false)
const selectedPost = ref(null)
const comments = ref([])
const commentsLoading = ref(false)
const commentText = ref('')
const commentSubmitting = ref(false)
const replyingTo = ref(null)
const replyText = ref('')

// 发帖 / 编辑 / 举报 / 创建版块
const composerOpen = ref(false)
const editingPost = ref(null)
const reportTarget = ref(null)
const reportReason = ref('')
const forumCreatorOpen = ref(false)
const creatingForum = ref(false)
const forumForm = ref({
  forumName: '',
  description: '',
})

// 收藏夹选择器
const folderPickerOpen = ref(false)
const folderPickerTarget = ref(null)
const pickerFolderName = ref('')

// 各列表页面注册自己的句柄，用于点赞/收藏后跨列表同步、操作后统一刷新
const feedHandles = new Set()
const registerFeed = (handle) => {
  feedHandles.add(handle)
  return () => feedHandles.delete(handle)
}

const patchMountedPosts = (postId, patch) => {
  for (const handle of feedHandles) {
    handle.applyPostPatch?.(postId, patch)
  }
}

const reloadMountedFeeds = () => {
  for (const handle of feedHandles) {
    handle.reload?.()
  }
}

const loadForums = async () => {
  const res = await getForums()
  forums.value = res.data
}

const loadFavoriteFolders = async () => {
  const res = await getFavoriteFolders()
  favoriteFolders.value = res.data
}

const initialize = async () => {
  try {
    await Promise.all([loadForums(), loadFavoriteFolders()])
  } catch (e) {
    error.value = '无法加载论坛数据: ' + (e.response?.data?.message || e.message)
  }
}

// ---- 版块创建 ----

const openForumCreator = () => {
  error.value = null
  forumCreatorOpen.value = true
}

const closeForumCreator = () => {
  if (creatingForum.value) return
  forumCreatorOpen.value = false
}

const handleCreateForum = async (router) => {
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
    if (res.data?.forumID) {
      await router.push(`/forums/board/${res.data.forumID}`)
    }
  } catch (e) {
    error.value = '创建版块失败: ' + (e.response?.data?.message || e.message)
  } finally {
    creatingForum.value = false
  }
}

// ---- 发帖 ----

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

// ---- 点赞 / 收藏 ----

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

const handleLike = async (post) => {
  const res = post.isLiked ? await unlikePost(post.postID) : await likePost(post.postID)
  updatePostLikeState(post.postID, res.data.liked, res.data.likeCount)
}

const handleFavorite = async (post) => {
  if (post.isFavorited) {
    await unfavoritePost(post.postID)
    updatePostFavoriteState(post.postID, false)
    await loadFavoriteFolders()
    reloadMountedFeeds()
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

// ---- 帖子删除 / 编辑 ----

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

// ---- 举报 ----

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

// ---- 帖子详情与评论 ----

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

const loadPostDetail = async (postId) => {
  try {
    detailLoading.value = true
    error.value = null
    const res = await getPost(postId)
    selectedPost.value = res.data
    await loadComments()
  } catch (e) {
    error.value = '无法加载帖子详情: ' + (e.response?.data?.message || e.message)
  } finally {
    detailLoading.value = false
  }
}

const cancelReply = () => {
  replyingTo.value = null
  replyText.value = ''
}

const closePostDetail = () => {
  selectedPost.value = null
  comments.value = []
  commentText.value = ''
  cancelReply()
}

const startReply = (comment) => {
  replyingTo.value = comment.commentID
  replyText.value = `@${comment.username || '用户'} `
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

export function useForum() {
  // useForum 始终在组件 setup 中调用，可安全获取 router
  const router = useRouter()

  return {
    // 数据
    forums,
    favoriteFolders,
    error,
    notice,
    loadForums,
    loadFavoriteFolders,
    initialize,
    registerFeed,

    // 版块创建
    forumCreatorOpen,
    creatingForum,
    forumForm,
    openForumCreator,
    closeForumCreator,
    createForumSubmit: () => handleCreateForum(router),

    // 发帖
    composerOpen,
    openComposer,
    closeComposer,
    handlePostCreated,

    // 点赞 / 收藏
    handleLike,
    handleFavorite,
    folderPickerOpen,
    pickerFolderName,
    selectFolderForFavorite,
    handlePickerCreateFolder,
    closeFolderPicker,

    // 删除 / 编辑
    handleDeletePost,
    editingPost,
    openEditPost,
    closeEditPost,
    handleEditorSaved,

    // 举报
    reportTarget,
    reportReason,
    openReport,
    openCommentReport,
    handleCreateReport,

    // 详情与评论（详情页使用）
    detailLoading,
    selectedPost,
    comments,
    commentsLoading,
    commentText,
    commentSubmitting,
    replyingTo,
    replyText,
    loadPostDetail,
    closePostDetail,
    handleCreateComment,
    startReply,
    cancelReply,
    handleDeleteComment,
  }
}
