<template>
  <ModalDialog
    :visible="open"
    variant="panel"
    kicker="新建版块"
    title="创建新的讨论空间"
    subtitle="使用清晰的名称和描述，让同学容易找到对应话题。"
    tag="form"
    dialog-class="forum-creator-modal"
    @close="$emit('close')"
    @submit="$emit('submit')"
  >
    <div class="forum-create-fields">
      <label>
        版块名称
        <input :value="form.forumName" type="text" minlength="2" maxlength="100" required placeholder="例如：校园摄影" @input="$emit('update:form', { ...form, forumName: $event.target.value })" />
      </label>
      <label>
        版块描述
        <textarea :value="form.description" maxlength="500" rows="4" placeholder="简要说明这里适合讨论什么" @input="$emit('update:form', { ...form, description: $event.target.value })"></textarea>
      </label>
    </div>

    <template #actions>
      <button class="btn" type="button" :disabled="creating" @click="$emit('close')">取消</button>
      <button class="btn btn-primary" type="submit" :disabled="creating">
        {{ creating ? '创建中...' : '创建版块' }}
      </button>
    </template>
  </ModalDialog>
</template>

<script setup>
import ModalDialog from './ModalDialog.vue'

defineProps({
  open: { type: Boolean, default: false },
  form: { type: Object, required: true },
  creating: { type: Boolean, default: false },
})

defineEmits(['close', 'submit', 'update:form'])
</script>

<style scoped>
.forum-create-fields {
  display: grid;
  gap: 1rem;
}

.forum-create-fields label {
  color: var(--text-secondary);
  display: grid;
  font-size: 0.82rem;
  font-weight: 700;
  gap: 0.45rem;
}

.forum-create-fields input,
.forum-create-fields textarea {
  border: 1px solid var(--border);
  border-radius: 12px;
  font: inherit;
  padding: 0.8rem 0.9rem;
  resize: vertical;
}
</style>
