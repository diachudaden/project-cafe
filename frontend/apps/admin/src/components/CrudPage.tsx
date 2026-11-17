import { useEffect, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { App, Button, Card, Form, Modal, Popconfirm, Space, Table, type TableColumnsType } from "antd";
import { ApiError, type CrudResource } from "@cafecr/api-client";

const PAGE_SIZE = 20;

interface CrudPageProps<T extends { id: string }, TForm, TCreate, TUpdate, TDetail> {
  title: string;
  queryKey: string;
  resource: CrudResource<T, TCreate, TUpdate, TDetail>;
  columns: TableColumnsType<T>;
  /** Form.Item fields shown in the create/edit dialog. */
  renderFields: (mode: "create" | "edit") => ReactNode;
  initialValues: Partial<TForm>;
  /** Loads the form values for editing; may fetch the detail endpoint. */
  toFormValues: (item: T) => TForm | Promise<TForm>;
  toCreate: (values: TForm) => TCreate;
  toUpdate: (values: TForm) => TUpdate;
  /** Extra list query parameters (filters); changing them goes back to page 1. */
  listParams?: Record<string, string | string[] | undefined>;
  /** Filter controls shown above the table. */
  toolbar?: ReactNode;
}

// Backend validation keys are PascalCase DTO property names ("Name", "OpenTime").
const toFieldName = (key: string) => key.charAt(0).toLowerCase() + key.slice(1);

export function CrudPage<T extends { id: string }, TForm extends object, TCreate, TUpdate, TDetail>(
  props: CrudPageProps<T, TForm, TCreate, TUpdate, TDetail>,
) {
  const { resource, queryKey } = props;
  const { message } = App.useApp();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<TForm>();
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<{ mode: "create" } | { mode: "edit"; id: string } | null>(null);

  const paramsKey = JSON.stringify(props.listParams ?? {});
  useEffect(() => setPage(1), [paramsKey]);

  const list = useQuery({
    queryKey: [queryKey, page, paramsKey],
    queryFn: () => resource.list(page, PAGE_SIZE, props.listParams),
    placeholderData: (previous) => previous,
  });

  const save = useMutation({
    mutationFn: (values: TForm) =>
      editing?.mode === "edit"
        ? resource.update(editing.id, props.toUpdate(values))
        : resource.create(props.toCreate(values)),
    onSuccess: () => {
      message.success("Đã lưu.");
      setEditing(null);
      void queryClient.invalidateQueries({ queryKey: [queryKey] });
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        const fields = Object.entries(error.fieldErrors).map(([key, errors]) => ({ name: toFieldName(key), errors }));
        // Only mark fields that exist in the form; anything else goes to the toast.
        const known = fields.filter((field) => form.getFieldInstance(field.name as never) !== undefined);
        form.setFields(known as never);
        if (known.length === fields.length && known.length > 0) return;
      }
      message.error(error.message);
    },
  });

  const remove = useMutation({
    mutationFn: (id: string) => resource.remove(id),
    onSuccess: () => {
      message.success("Đã xóa.");
      void queryClient.invalidateQueries({ queryKey: [queryKey] });
    },
    onError: (error) => message.error(error.message),
  });

  function openCreate() {
    form.resetFields();
    form.setFieldsValue(props.initialValues as never);
    setEditing({ mode: "create" });
  }

  async function openEdit(item: T) {
    try {
      const values = await props.toFormValues(item);
      form.resetFields();
      form.setFieldsValue(values as never);
      setEditing({ mode: "edit", id: item.id });
    } catch (error) {
      message.error(error instanceof Error ? error.message : "Không tải được dữ liệu.");
    }
  }

  const items = list.data?.items ?? [];
  // The API returns no total count, so a full page is the only hint that another one exists.
  const hasNext = items.length === PAGE_SIZE;

  return (
    <Card
      title={props.title}
      extra={
        <Button type="primary" onClick={openCreate}>
          Thêm mới
        </Button>
      }
    >
      {props.toolbar && <div style={{ marginBottom: 16 }}>{props.toolbar}</div>}
      <Table<T>
        rowKey="id"
        size="middle"
        scroll={{ x: "max-content" }}
        loading={list.isFetching}
        dataSource={items}
        pagination={false}
        locale={list.isError ? { emptyText: list.error.message } : undefined}
        columns={[
          ...props.columns,
          {
            key: "actions",
            title: "",
            fixed: "right",
            render: (_, item) => (
              <Space>
                <Button size="small" onClick={() => void openEdit(item)}>
                  Sửa
                </Button>
                <Popconfirm title="Xóa bản ghi này?" okText="Xóa" cancelText="Hủy" onConfirm={() => remove.mutate(item.id)}>
                  <Button size="small" danger>
                    Xóa
                  </Button>
                </Popconfirm>
              </Space>
            ),
          },
        ]}
      />
      <Space style={{ marginTop: 16 }}>
        <Button disabled={page === 1} onClick={() => setPage((p) => p - 1)}>
          Trang trước
        </Button>
        <span>Trang {page}</span>
        <Button disabled={!hasNext} onClick={() => setPage((p) => p + 1)}>
          Trang sau
        </Button>
      </Space>

      <Modal
        open={editing !== null}
        title={editing?.mode === "edit" ? `Sửa ${props.title.toLowerCase()}` : `Thêm ${props.title.toLowerCase()}`}
        okText="Lưu"
        cancelText="Hủy"
        confirmLoading={save.isPending}
        onOk={() => form.submit()}
        onCancel={() => setEditing(null)}
        destroyOnClose
        forceRender
      >
        <Form<TForm> form={form} layout="vertical" onFinish={(values) => save.mutate(values)}>
          {editing && props.renderFields(editing.mode)}
        </Form>
      </Modal>
    </Card>
  );
}
