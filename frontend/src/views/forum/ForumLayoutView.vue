<template>
  <div class="page-container forum-page">
    <MessagePopup :message="error" type="error" @close="error = null" />
    <MessagePopup :message="notice" type="success" @close="notice = ''" />

    <ForumCreatorModal
      :open="forumCreatorOpen"
      :form="forumForm"
      :creating="creatingForum"
      @close="closeForumCreator"
      @submit="createForumSubmit"
      @update:form="forumForm = $event"
    />

    <div class="forum-layout">
      <aside class="forum-sidebar">
        <div class="forum-section-nav">
          <button type="button" :class="['forum-section-link', { active: !isMySection }]" @click="goHome"><span class="filter-icon">⌂</span><span>首页</span></button>
          <button type="button" :class="['forum-section-link', { active: isMySection }]" @click="goMy"><span class="filter-icon">◉</span><span>我的</span></button>
        </div>
        <div class="section-eyebrow">CAMPUS COMMUNITY</div>
        <button v-for="forum in forums" :key="forum.forumID" :class="['forum-filter', { active: selectedForumId === forum.forumID }]" @click="goBoard(forum.forumID)"><img v-if="canShowAvatar(forum.avatarUrl)" :src="forum.avatarUrl" :alt="forum.forumName" class="forum-filter-avatar" @error="markAvatarFailed(forum.avatarUrl)" /><span v-else class="filter-icon">#</span><span class="forum-filter-name">{{ forum.forumName }}</span><span class="forum-count">{{ forum.postCount || 0 }}</span></button>
      </aside>
      <main class="forum-main">
        <router-view v-slot="{ Component }">
          <keep-alive :include="['ForumHomeView', 'ForumBoardView', 'ForumSearchView', 'MyView']">
            <component :is="Component" />
          </keep-alive>
        </router-view>
      </main>
    </div>

    <PostComposerModal
      :open="composerOpen"
      :forums="forums"
      @close="closeComposer"
      @created="handlePostCreated"
      @error="error = $event"
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
      @close="closeFolderPicker"
      @save="handlePickerSaveFolders"
    />
  </div>
</template>

<script setup>
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ForumCreatorModal from '../../components/forum/ForumCreatorModal.vue'
import MessagePopup from '../../components/MessagePopup.vue'
import PostComposerModal from '../../components/forum/PostComposerModal.vue'
import PostEditorModal from '../../components/forum/PostEditorModal.vue'
import ReportModal from '../../components/forum/ReportModal.vue'
import FolderPickerModal from '../../components/forum/FolderPickerModal.vue'
import { useForum } from '../../composables/useForum'
import { canShowAvatar, markAvatarFailed } from '../../utils/avatarFallback'

const {
  forums,
  favoriteFolders,
  error,
  notice,
  initialize,
  forumCreatorOpen,
  creatingForum,
  forumForm,
  closeForumCreator,
  createForumSubmit,
  composerOpen,
  closeComposer,
  handlePostCreated,
  folderPickerOpen,
  handlePickerSaveFolders,
  closeFolderPicker,
  editingPost,
  closeEditPost,
  handleEditorSaved,
  reportTarget,
  reportReason,
  handleCreateReport,
} = useForum()

const route = useRoute()
const router = useRouter()

const isMySection = computed(() => route.path.startsWith('/forums/my'))
const selectedForumId = computed(() => (route.params.forumId ? Number(route.params.forumId) : null))

const goHome = () => {
  if (route.path !== '/forums') router.push('/forums')
}

const goMy = () => {
  if (!isMySection.value) router.push('/forums/my')
}

const goBoard = (forumId) => {
  if (selectedForumId.value !== forumId) router.push(`/forums/board/${forumId}`)
}

onMounted(() => {
  initialize()
})
</script>

<style scoped>
.forum-page {
  --forum-ink: #11172a;
  --tieba-blue: #2f7ee0;
  --tieba-blue-dark: #266bc4;
  --tieba-blue-light: #e6f1fc;
  width: 100%;
  max-width: 1480px;
  margin: 0 auto;
  color: var(--forum-ink);
}

.forum-layout {
  display: grid;
  grid-template-columns: 224px minmax(0, 1fr);
  gap: 1.25rem;
  align-items: start;
  justify-content: stretch;
}

.forum-main {
  min-width: 0;
}

.forum-sidebar { background: var(--surface); border: 1px solid #e4e7ec; border-radius: 14px; padding: .85rem .7rem; position: sticky; top: 1rem; }
.forum-section-nav { margin: -.15rem 0 .75rem; padding-bottom: .65rem; border-bottom: 1px solid #edf0f4; }
.forum-section-link { width: 100%; display: flex; align-items: center; gap: .55rem; padding: .55rem .65rem; border: 0; border-radius: 8px; background: transparent; color: #687286; cursor: pointer; font: inherit; font-size: .82rem; font-weight: 700; text-align: left; }
.forum-section-link:hover, .forum-section-link.active { background: #eef4ff; color: #266bc4; }
.section-eyebrow { padding: .25rem .65rem 0; color: #9aa3b3; font-size: .58rem; font-weight: 800; letter-spacing: .13em; }
.forum-filter { width: 100%; min-height: 44px; margin: .1rem 0; display: flex; align-items: center; gap: .35rem; border: 0; border-radius: 9px; padding: .6rem .7rem; color: var(--text); background: transparent; cursor: pointer; font-size: .82rem; text-align: left; }
.forum-filter:hover, .forum-filter.active { background: #f2f7fd; }
.forum-filter.active { color: var(--tieba-blue-dark); font-weight: 700; }
.filter-icon { width: 1.25rem; color: #9aa4b4; text-align: center; }
.forum-filter-avatar { width: 24px; height: 24px; flex: 0 0 auto; border-radius: 6px; object-fit: cover; background: #eef1f6; }
.forum-filter.active .filter-icon { color: var(--tieba-blue); }
.forum-filter-name { min-width: 0; overflow: hidden; flex: 1; text-overflow: ellipsis; white-space: nowrap; }
.forum-count { color: #a1aabd; font-size: .7rem; }

@media (max-width: 820px) {
  .forum-layout {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .forum-sidebar { position: static; overflow-x: auto; display: flex; align-items: center; gap: .35rem; padding: .55rem; }
  .forum-filter { width: auto; min-width: max-content; min-height: 36px; margin: 0; padding: .45rem .65rem; }
  .forum-filter-avatar { width: 20px; height: 20px; }
  .forum-count { display: none; }
}
</style>
