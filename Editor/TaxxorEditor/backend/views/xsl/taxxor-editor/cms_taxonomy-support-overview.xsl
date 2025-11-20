<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
    <xsl:include href="_utils.xsl"/>

    <xsl:param name="app-config"/>
    <xsl:param name="group-by-client">no</xsl:param>

    <xsl:output method="html" indent="yes"/>


    <xsl:template match="/">
        <xsl:choose>
            <xsl:when test="$group-by-client='yes'">
                <xsl:apply-templates select="clients/client/entity_groups/entity_group">
                    <xsl:sort select="name" data-type="text"/>
                </xsl:apply-templates>
            </xsl:when>
            <xsl:otherwise>
                <xsl:apply-templates select="clients/reporting_requirements"> </xsl:apply-templates>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>

    <xsl:template match="entity_group">
        <xsl:apply-templates select="entity">
            <xsl:with-param name="entity_group_name" select="name"/>
        </xsl:apply-templates>
    </xsl:template>

    <xsl:template match="entity">
        <xsl:param name="entity_group_name"/>

        <div class="panel panel-default">
            <div class="panel-heading">
                <h3 class="panel-title">
                    <xsl:value-of select="$entity_group_name"/>
                    <xsl:text disable-output-escaping="yes"> &gt; </xsl:text>
                    <xsl:value-of select="name"/>
                </h3>
            </div>
            <div class="panel-body">
                <xsl:choose>
                    <xsl:when test="count(reporting_requirements/reporting_requirement)=0">
                        <div class="alert alert-warning">
                            <strong>
                                <xsl:call-template name="get-localized-value-by-key">
                                    <xsl:with-param name="doc-translations" select="$app-config"/>
                                    <xsl:with-param name="id">
                                        <xsl:text>warning</xsl:text>
                                    </xsl:with-param>
                                </xsl:call-template>
                                <xsl:text>: </xsl:text>
                            </strong>
                            <xsl:call-template name="get-localized-value-by-key">
                                <xsl:with-param name="doc-translations" select="$app-config"/>
                                <xsl:with-param name="id">
                                    <xsl:text>frag_no-reporting-requirements-defined</xsl:text>
                                </xsl:with-param>
                            </xsl:call-template>
                            <br/>
                            <xsl:call-template name="get-localized-value-by-key">
                                <xsl:with-param name="doc-translations" select="$app-config"/>
                                <xsl:with-param name="id">
                                    <xsl:text>frag_no-check-regulationdb-add-requirements</xsl:text>
                                </xsl:with-param>
                            </xsl:call-template>
                        </div>

                    </xsl:when>

                    <xsl:otherwise>
                        <xsl:apply-templates select="reporting_requirements"/>
                    </xsl:otherwise>
                </xsl:choose>
            </div>
        </div>

    </xsl:template>

    <xsl:template match="reporting_requirements">
        <div class="cms_taxonomy-support-overview">
            <table class="table table-striped">
                <thead>
                    <tr class="info">
                        <th class="col1">
                            <xsl:call-template name="get-localized-value-by-key">
                                <xsl:with-param name="doc-translations" select="$app-config"/>
                                <xsl:with-param name="id">
                                    <xsl:text>label_taxonomy</xsl:text>
                                </xsl:with-param>
                            </xsl:call-template>
                        </th>
                        <th class="col2">
                            <xsl:call-template name="get-localized-value-by-key">
                                <xsl:with-param name="doc-translations" select="$app-config"/>
                                <xsl:with-param name="id">
                                    <xsl:text>label_editor</xsl:text>
                                </xsl:with-param>
                            </xsl:call-template>
                        </th>
                        <th class="col3">
                            <xsl:call-template name="get-localized-value-by-key">
                                <xsl:with-param name="doc-translations" select="$app-config"/>
                                <xsl:with-param name="id">
                                    <xsl:text>label_statistics</xsl:text>
                                </xsl:with-param>
                            </xsl:call-template>
                        </th>
                        <th class="col4">
                            <xsl:text> </xsl:text>
                        </th>
                    </tr>
                </thead>
                <tbody>
                    <xsl:apply-templates select="reporting_requirement">
                        <xsl:sort select="@ready" data-type="text" order="descending"/>
                        <xsl:sort select="name" data-type="text" order="ascending"/>
                    </xsl:apply-templates>
                </tbody>
            </table>
        </div>


    </xsl:template>


    <xsl:template match="reporting_requirement">
        <xsl:variable name="entry-point" select="requirement/entrypoint/location"/>
        <xsl:variable name="report-type-id" select="$app-config//report_types/report_type[entry_points/uri=$entry-point]/@id"/>
        <xsl:variable name="editor-id" select="$app-config//report_types/report_type[entry_points/uri=$entry-point]/@editorId"/>
        <xsl:variable name="editor-name">
            <xsl:choose>
                <xsl:when test="$app-config">
                    <xsl:value-of select="$app-config//editors/editor[@id=$editor-id]/name"/>
                </xsl:when>
                <xsl:otherwise>
                    <xsl:text>[no-name]</xsl:text>
                </xsl:otherwise>
            </xsl:choose>
        </xsl:variable>
        <xsl:variable name="nodelist-projects" select="$app-config//cms_projects/cms_project[@report-type=$report-type-id]"/>

        <tr>
            <td>
                <a target="_blank">
                    <xsl:attribute name="href">
                        <xsl:value-of select="requirement/report/web"/>
                    </xsl:attribute>
                    <xsl:value-of select="name"/>
                </a>
            </td>
            <td>
                <xsl:value-of select="$editor-name"/>
            </td>
            <td>
                <xsl:if test="$app-config">
                    <xsl:choose>
                        <xsl:when test="count($nodelist-projects)=0">
                            <xsl:call-template name="get-localized-value-by-key">
                                <xsl:with-param name="doc-translations" select="$app-config"/>
                                <xsl:with-param name="id">
                                    <xsl:text>frag_stats-not-in-use</xsl:text>
                                </xsl:with-param>
                            </xsl:call-template>
                        </xsl:when>
                        <xsl:when test="count($nodelist-projects)=1">
                            <xsl:call-template name="get-localized-value-by-key">
                                <xsl:with-param name="doc-translations" select="$app-config"/>
                                <xsl:with-param name="id">
                                    <xsl:text>frag_used-by</xsl:text>
                                </xsl:with-param>
                            </xsl:call-template>
                            <xsl:text> </xsl:text>
                            <xsl:value-of select="count($nodelist-projects)"/>
                            <xsl:text> </xsl:text>
                            <xsl:call-template name="get-localized-value-by-key">
                                <xsl:with-param name="doc-translations" select="$app-config"/>
                                <xsl:with-param name="id">
                                    <xsl:text>frag_filing</xsl:text>
                                </xsl:with-param>
                            </xsl:call-template>
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:call-template name="get-localized-value-by-key">
                                <xsl:with-param name="doc-translations" select="$app-config"/>
                                <xsl:with-param name="id">
                                    <xsl:text>frag_used-by</xsl:text>
                                </xsl:with-param>
                            </xsl:call-template>
                            <xsl:text> </xsl:text>
                            <xsl:value-of select="count($nodelist-projects)"/>
                            <xsl:text> </xsl:text>
                            <xsl:call-template name="get-localized-value-by-key">
                                <xsl:with-param name="doc-translations" select="$app-config"/>
                                <xsl:with-param name="id">
                                    <xsl:text>frag_filings</xsl:text>
                                </xsl:with-param>
                            </xsl:call-template>
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:if>

            </td>
            <td>
                <div class="pull-right">
                    <!--
                    <xsl:value-of select="@ready"/>
                    -->
                    <xsl:if test="$app-config">
                        <xsl:comment>
                        <xsl:text>report-type-id: </xsl:text><xsl:value-of select="$report-type-id"/>
                        </xsl:comment>

                        <xsl:for-each select="$nodelist-projects">
                            <xsl:comment>
                                <xsl:text>project-id: </xsl:text><xsl:value-of select="./@id"/>
                            </xsl:comment>
                        </xsl:for-each>

                        <xsl:comment>
                        <xsl:text>editor-id: </xsl:text><xsl:value-of select="$editor-id"/>
                        </xsl:comment>
                    </xsl:if>

                    <!-- create a javascript object string for the filings involved -->
                    <xsl:variable name="project-data">
                        <xsl:text>[</xsl:text>
                        <xsl:if test="$app-config">
                            <xsl:for-each select="$nodelist-projects">
                                <xsl:text>{</xsl:text>
                                <xsl:text>id: '</xsl:text>
                                <xsl:value-of select="./@id"/>
                                <xsl:text>', </xsl:text>
                                <xsl:text>name: '</xsl:text>
                                <xsl:value-of select="./name"/>
                                <xsl:text>'</xsl:text> 
                                <xsl:text>}</xsl:text>
                                <xsl:if test="not(position()=last())">
                                    <xsl:text>, </xsl:text>
                                </xsl:if>
                            </xsl:for-each>
                        </xsl:if>
                        <xsl:text>]</xsl:text>
                    </xsl:variable>

                    <xsl:choose>
                        <xsl:when test="string(@ready)='false'">
                            <!-- show create button -->
                            <button class="btn btn-primary btn-xs" onclick="openNewTaxoSupportModal('{requirement/entrypoint/location}', '{name}')">
                                <span class="glyphicon glyphicon-plus-sign"> </span>
                                <xsl:text> </xsl:text>
                                <xsl:call-template name="get-localized-value-by-key">
                                    <xsl:with-param name="doc-translations" select="$app-config"/>
                                    <xsl:with-param name="id">
                                        <xsl:text>btn-text_add-support</xsl:text>
                                    </xsl:with-param>
                                </xsl:call-template>
                            </button>
                        </xsl:when>
                        <xsl:otherwise>
                            <!-- show delete and settings button -->
                            <button class="btn btn-xs btn-default" onclick="objPageVars.projectdata={$project-data};openTaxoSupportPropertiesModal('{requirement/entrypoint/location}', '{name}', '{$editor-id}')">
                                <i class="ace-icon fa fa-cog bigger-110"> </i>
                                <xsl:text> </xsl:text>
                                <xsl:call-template name="get-localized-value-by-key">
                                    <xsl:with-param name="doc-translations" select="$app-config"/>
                                    <xsl:with-param name="id">
                                        <xsl:text>btn-text_properties</xsl:text>
                                    </xsl:with-param>
                                </xsl:call-template>
                            </button>
                            <xsl:text> </xsl:text>
                            <button onclick="objPageVars.taxoentrypoint='{requirement/entrypoint/location}';objPageVars.taxoname='{name}';objPageVars.projectdata={$project-data}" class="btn btn-danger btn-xs" data-target="#deleteTaxoSupportModal" data-toggle="modal">
                                <span class="glyphicon glyphicon-trash"> </span>
                                <xsl:text> </xsl:text>
                                <xsl:call-template name="get-localized-value-by-key">
                                    <xsl:with-param name="doc-translations" select="$app-config"/>
                                    <xsl:with-param name="id">
                                        <xsl:text>btn-text_remove-support</xsl:text>
                                    </xsl:with-param>
                                </xsl:call-template>
                            </button>
                        </xsl:otherwise>
                    </xsl:choose>


                </div>
            </td>

        </tr>
    </xsl:template>


</xsl:stylesheet>
