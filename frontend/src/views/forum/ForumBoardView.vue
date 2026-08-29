<template>
  <ForumFeed :forum-id="forumId" :forum="forum" />
</template>

<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import ForumFeed from '../../components/forum/ForumFeed.vue'
import { useForum } from '../../composables/useForum'

const route = useRoute()
const { forums } = useForum()

const forumId = computed(() => Number(route.params.forumId))
const forum = computed(() => forums.value.find(f => f.forumID === forumId.value) || null)
</script>
