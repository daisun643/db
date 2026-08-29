<template>
  <div class="favorites-layout">
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
      <div v-else class="post-list tieba-panel">
        <ForumPostCard
          v-for="post in favoritePosts"
          :key="post.postID"
          :post="post"
          mode="favorite"
          @open="goPostDetail"
          @like="handleLike"
          @remove-favorite="handleRemoveFavorite"
        />
        <div v-if="favoritePosts.length === 0" class="empty-state">
          <p>暂无收藏帖子</p>
        </div>
      </div>
    </main>
  </div>
</template>

<script setup>
import { onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import ForumPostCard from '../../components/forum/ForumPostCard.vue'
import { useForum } from '../../composables/useForum'
import {
  createFavoriteFolder,
  deleteFavoriteFolder,
  getFavoriteFolderPosts,
  removePostFromFavoriteFolder,
  updateFavoriteFolder,
} from '../../api'

const { error, favoriteFolders, loadFavoriteFolders, handleLike, registerFeed } = useForum()

const router = useRouter()
const goPostDetail = (post) => router.push(`/forums/post/${post.postID}`)

const favoritePosts = ref([])
const loadingFavorites = ref(false)
const selectedFolderId = ref('')
const folderName = ref('')
const renamingFolderId = ref(null)
const renameText = ref('')

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

const selectFolder = async (folderId) => {
  selectedFolderId.value = folderId
  await loadFavoritePosts()
}

const handleCreateFolder = async () => {
  const res = await createFavoriteFolder({ folderName: folderName.value })
  folderName.value = ''
  await loadFavoriteFolders()
  selectedFolderId.value = res.data.folderID
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
}

const handleRemoveFavorite = async (post) => {
  if (!selectedFolderId.value) return
  await removePostFromFavoriteFolder(selectedFolderId.value, post.postID)
  await loadFavoriteFolders()
  await loadFavoritePosts()
}

const applyPostPatch = (postId, patch) => {
  const target = favoritePosts.value.find(item => item.postID === postId)
  if (target) {
    Object.assign(target, patch)
  }
}

let unregisterFeed = null

onMounted(() => {
  unregisterFeed = registerFeed({ reload: loadFavoritePosts, applyPostPatch })
  loadFavoritePosts()
})

onUnmounted(() => {
  unregisterFeed?.()
})
</script>

<style scoped>
.favorites-layout {
  display: grid;
  grid-template-columns: 250px minmax(0, 1fr);
  gap: 1rem;
  align-items: start;
  justify-content: stretch;
}

.favorites-sidebar {
  background: var(--surface);
  border: 1px solid rgba(25, 34, 59, .08);
  border-radius: 14px;
  padding: .75rem;
  position: sticky;
  top: 1rem;
}

.section-title {
  padding: .45rem .65rem .55rem;
  color: #8990a0;
  font-size: .68rem;
  font-weight: 700;
  letter-spacing: .08em;
  text-transform: uppercase;
}

.favorites-main {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.favorites-main > .post-list {
  margin-top: 0;
}

.folder-create-form {
  display: grid;
  grid-template-columns: 1fr;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}

.folder-create-form input {
  border: 1px solid #e2e4ed;
  border-radius: 11px;
  background: #fafafd;
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
  margin: .2rem 0;
  border-radius: 12px;
  cursor: pointer;
  padding: 0.625rem 0.75rem;
  transition: background 0.15s;
}

.folder-item:hover {
  background: #f6f4ff;
}

.folder-item.active {
  color: #5d4dd7;
  background: #efedff;
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
  opacity: .68;
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

.post-list {
  margin-top: .9rem;
  border: 1px solid #e4e7ec;
  border-radius: 6px;
  background: #fff;
  overflow: hidden;
}

.empty-state {
  padding: 3rem 1rem;
  color: var(--text-secondary);
  text-align: center;
}

@media (max-width: 1000px) {
  .favorites-layout {
    grid-template-columns: 215px minmax(0, 1fr);
  }
}

@media (max-width: 820px) {
  .favorites-layout {
    grid-template-columns: 1fr;
  }

  .favorites-sidebar {
    position: static;
  }
}
</style>
